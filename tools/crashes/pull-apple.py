#!/usr/bin/env python
"""Slice 227: Apple's own crash data for Dino Reader and Read The Stupid Text, through the App Store Connect API.

    python -I tools/crashes/pull-apple.py [--app dino|rtst|both] [--builds 40] [--out DIR]

For each app it reads (all read-only GETs):
  * the newest builds, and for each build its diagnostic signatures (hangs, disk writes, launches; Xcode Organizer's data, which exists only for
    builds that reached the App Store, so a TestFlight-only app mostly answers 404 or empty) with the logs of each signature;
  * TestFlight crash submissions (betaFeedbackCrashSubmissions, sent only when a tester taps Share in TestFlight's crash prompt) with each crash log.
It prints a summary (signatures by weight, TestFlight crashes by date, device and OS) and saves every raw answer as JSON under --out (default: <temp>/rtst-apple-crashes).

Needs: pip install pyjwt cryptography requests
The App Store Connect key ("LCM CI", App Manager role, shared account) is read from D:\\secrets\\lets-call-mom (secrets.env holds ASC_API_KEY_ID and
ASC_API_ISSUER_ID; the AuthKey .p8 file sits next to it). Override the folder with ASC_SECRETS_DIR. The key and the token are never printed.
"""
import argparse
import glob
import json
import os
import re
import sys
import tempfile
import time

import jwt
import requests

BASE = "https://api.appstoreconnect.apple.com"
SECRETS_DIR = os.environ.get("ASC_SECRETS_DIR", r"D:\secrets\lets-call-mom")
APP_BUNDLES = {"dino": "uk.sirous.dinoreader", "rtst": "uk.sirous.readthestupidtext"}
TOKEN_LIFETIME_SECONDS = 1100
TIMEOUT_SECONDS = 60


def secret(env_text, name):
    match = re.search(r"^%s=(.*)$" % re.escape(name), env_text, re.M)
    if not match:
        sys.exit("Missing %s in %s" % (name, os.path.join(SECRETS_DIR, "secrets.env")))
    return match.group(1).strip().strip('"').strip("'")


class AppStoreConnect:
    """A tiny read-only client: one fresh JWT per request, nothing logged but the status and the path."""

    def __init__(self, out_dir):
        env_text = open(os.path.join(SECRETS_DIR, "secrets.env"), encoding="utf-8").read()
        self.key_id = secret(env_text, "ASC_API_KEY_ID")
        self.issuer = secret(env_text, "ASC_API_ISSUER_ID")
        key_files = glob.glob(os.path.join(SECRETS_DIR, "AppStoreConnect_AuthKey_*.p8"))
        if not key_files:
            sys.exit("No AppStoreConnect_AuthKey_*.p8 in %s" % SECRETS_DIR)
        self.key = open(key_files[0], encoding="utf-8").read()
        self.out_dir = out_dir

    def token(self):
        now = int(time.time())
        claims = {"iss": self.issuer, "iat": now, "exp": now + TOKEN_LIFETIME_SECONDS, "aud": "appstoreconnect-v1"}
        return jwt.encode(claims, self.key, algorithm="ES256", headers={"kid": self.key_id, "typ": "JWT"})

    def get(self, path, params=None, save_as=None):
        response = requests.get(BASE + path, params=params, headers={"Authorization": "Bearer " + self.token()}, timeout=TIMEOUT_SECONDS)
        print(response.status_code, path, params or "")
        if save_as:
            with open(os.path.join(self.out_dir, save_as), "w", encoding="utf-8") as handle:
                handle.write(response.text)
        try:
            return response.status_code, response.json()
        except ValueError:
            return response.status_code, response.text


def newest_builds(asc, app_id, limit, tag):
    params = {
        "filter[app]": app_id, "limit": limit, "sort": "-uploadedDate", "include": "preReleaseVersion",
        "fields[builds]": "version,uploadedDate,processingState,preReleaseVersion", "fields[preReleaseVersions]": "version",
    }
    status, builds = asc.get("/v1/builds", params, "%s_builds.json" % tag)
    if status != 200:
        return []
    short = {item["id"]: item["attributes"]["version"] for item in builds.get("included", [])}
    result = []
    for build in builds["data"]:
        pre = (build["relationships"]["preReleaseVersion"].get("data") or {}).get("id")
        result.append((build["id"], "%s_%s" % (short.get(pre), build["attributes"]["version"])))
    return result


def diagnostic_signatures(asc, builds, tag):
    summary = []
    for build_id, build_tag in builds:
        status, sigs = asc.get("/v1/builds/%s/diagnosticSignatures" % build_id, {"limit": 200}, "%s_sigs_%s.json" % (tag, build_tag))
        if status != 200:
            continue  # 404 for a build with no diagnostics data is the normal case for TestFlight-only builds
        for sig in sigs["data"]:
            attrs = sig["attributes"]
            summary.append((build_tag, attrs.get("diagnosticType"), attrs.get("weight"), attrs.get("signature")))
            asc.get("/v1/diagnosticSignatures/%s/logs" % sig["id"], None, "%s_siglogs_%s_%s.json" % (tag, build_tag, sig["id"]))
    return summary


def testflight_crashes(asc, app_id, tag):
    status, submissions = asc.get("/v1/apps/%s/betaFeedbackCrashSubmissions" % app_id, {"limit": 200}, "%s_tf_crashes.json" % tag)
    if status != 200 or not isinstance(submissions, dict):
        return []
    found = []
    for item in submissions.get("data", []):
        attrs = item["attributes"]
        found.append((attrs.get("createdDate"), attrs.get("deviceModel"), attrs.get("osVersion"), attrs.get("comment")))
        asc.get("/v1/betaFeedbackCrashSubmissions/%s/crashLog" % item["id"], None, "%s_tf_crashlog_%s.json" % (tag, item["id"]))
    return found


def pull_app(asc, name, bundle, builds_limit):
    status, apps = asc.get("/v1/apps", {"filter[bundleId]": bundle}, "%s_app.json" % name)
    if status != 200 or not apps.get("data"):
        print("== %s: app %s not found in App Store Connect" % (name, bundle))
        return
    app_id = apps["data"][0]["id"]
    print("== %s (%s) app id %s" % (name, bundle, app_id))
    builds = newest_builds(asc, app_id, builds_limit, name)
    signatures = diagnostic_signatures(asc, builds, name)
    crashes = testflight_crashes(asc, app_id, name)
    print("\n-- %s: %d build(s), %d diagnostic signature(s), %d TestFlight crash submission(s)" % (name, len(builds), len(signatures), len(crashes)))
    for build_tag, kind, weight, signature in sorted(signatures, key=lambda row: -(row[2] or 0)):
        print("   %s %-11s weight %s  %s" % (build_tag, kind, weight, signature))
    for created, device, os_version, comment in sorted(crashes, key=lambda row: str(row[0])):
        print("   TF crash %s  %s  iOS %s  %s" % (created, device, os_version, comment or ""))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--app", choices=["dino", "rtst", "both"], default="both")
    parser.add_argument("--builds", type=int, default=40, help="how many of the newest builds to check")
    parser.add_argument("--out", default=os.path.join(tempfile.gettempdir(), "rtst-apple-crashes"))
    args = parser.parse_args()
    os.makedirs(args.out, exist_ok=True)
    asc = AppStoreConnect(args.out)
    for name, bundle in APP_BUNDLES.items():
        if args.app in ("both", name):
            pull_app(asc, name, bundle, args.builds)
    print("\nRaw answers saved in %s" % args.out)


if __name__ == "__main__":
    main()
