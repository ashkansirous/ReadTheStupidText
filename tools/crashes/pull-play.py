#!/usr/bin/env python
"""Slice 227: Google Play's own crash and ANR data for Dino Reader and Read The Stupid Text, through the Play Developer Reporting API.

    python -I tools/crashes/pull-play.py [--app dino|rtst|both] [--days 7] [--out DIR]

For each app it lists the error issues (crashes and ANRs grouped by Play, newest first) and the error reports behind them:
    GET https://playdeveloperreporting.googleapis.com/v1beta1/apps/<package>/errorIssues:search
    GET https://playdeveloperreporting.googleapis.com/v1beta1/apps/<package>/errorReports:search
and prints the issues by report count. Raw answers are saved as JSON under --out (default: <temp>/rtst-play-crashes).

NOT RUN YET (Slice 227): written from the API reference without calling it. Needs, before it can work:
  * the OAuth scope  https://www.googleapis.com/auth/playdeveloperreporting  (this script asks for exactly that scope);
  * the Google Play Developer Reporting API enabled in the service account's Cloud project;
  * the service account invited in Play Console (Users and permissions) with the permission "View app information and download bulk reports (read-only)"
    for both apps (a Play Console permission change is the owner's; the account used by CI is auto-deploy-to-google-play@read-the-stupid-text.iam.gserviceaccount.com).
Credentials: the Play service account JSON in D:\\secrets\\readthestupidtext\\readthestupidtext-play-service-account.json (Dino reuses it, see D:\\secrets\\README.md);
override with PLAY_SERVICE_ACCOUNT_FILE. The key and the access token are never printed. Needs: pip install pyjwt cryptography requests
"""
import argparse
import datetime
import json
import os
import sys
import tempfile
import time

import jwt
import requests

SCOPE = "https://www.googleapis.com/auth/playdeveloperreporting"
API = "https://playdeveloperreporting.googleapis.com/v1beta1"
SERVICE_ACCOUNT_FILE = os.environ.get("PLAY_SERVICE_ACCOUNT_FILE", r"D:\secrets\readthestupidtext\readthestupidtext-play-service-account.json")
PACKAGES = {"dino": "uk.sirous.dinoreader", "rtst": "uk.sirous.readthestupidtext"}
PAGE_SIZE = 100
TIMEOUT_SECONDS = 60
GRANT_TYPE = "urn:ietf:params:oauth:grant-type:jwt-bearer"


def access_token():
    """Exchanges the service account's signed JWT for an access token with the reporting scope."""
    account = json.load(open(SERVICE_ACCOUNT_FILE, encoding="utf-8"))
    now = int(time.time())
    claims = {"iss": account["client_email"], "scope": SCOPE, "aud": account["token_uri"], "iat": now, "exp": now + 3000}
    assertion = jwt.encode(claims, account["private_key"], algorithm="RS256")
    response = requests.post(account["token_uri"], data={"grant_type": GRANT_TYPE, "assertion": assertion}, timeout=TIMEOUT_SECONDS)
    if response.status_code != 200:
        sys.exit("Token request failed (%s): %s" % (response.status_code, response.json().get("error_description", response.json().get("error"))))
    return response.json()["access_token"]


def interval_params(days):
    """The API's DateTimeInterval as query parameters: UTC, start and end hour-aligned."""
    end = datetime.datetime.now(datetime.timezone.utc).replace(minute=0, second=0, microsecond=0)
    start = end - datetime.timedelta(days=days)
    params = {}
    for name, moment in (("startTime", start), ("endTime", end)):
        for field in ("year", "month", "day", "hours"):
            params["interval.%s.%s" % (name, field)] = getattr(moment, field if field != "hours" else "hour")
        params["interval.%s.timeZone.id" % name] = "UTC"
    return params


def search(token, package, resource, days, name, out_dir):
    """Follows nextPageToken until the list ends; saves the pages and returns the items."""
    items, page_token, page = [], None, 0
    while True:
        params = {"pageSize": PAGE_SIZE, **interval_params(days)}
        if page_token:
            params["pageToken"] = page_token
        response = requests.get("%s/apps/%s/%s:search" % (API, package, resource), params=params, headers={"Authorization": "Bearer " + token}, timeout=TIMEOUT_SECONDS)
        print(response.status_code, package, resource)
        with open(os.path.join(out_dir, "%s_%s_%d.json" % (name, resource, page)), "w", encoding="utf-8") as handle:
            handle.write(response.text)
        if response.status_code != 200:
            return items
        body = response.json()
        items.extend(body.get(resource, []))
        page_token, page = body.get("nextPageToken"), page + 1
        if not page_token:
            return items


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--app", choices=["dino", "rtst", "both"], default="both")
    parser.add_argument("--days", type=int, default=7)
    parser.add_argument("--out", default=os.path.join(tempfile.gettempdir(), "rtst-play-crashes"))
    args = parser.parse_args()
    os.makedirs(args.out, exist_ok=True)
    token = access_token()
    for name, package in PACKAGES.items():
        if args.app not in ("both", name):
            continue
        issues = search(token, package, "errorIssues", args.days, name, args.out)
        reports = search(token, package, "errorReports", args.days, name, args.out)
        print("\n== %s (%s): %d issue(s), %d report(s) in the last %d day(s)" % (name, package, len(issues), len(reports), args.days))
        for issue in sorted(issues, key=lambda item: -int(item.get("errorReportCount", 0))):
            print("   %-6s %s reports, %s users  %s  %s" % (issue.get("type"), issue.get("errorReportCount"), issue.get("distinctUsers"), issue.get("cause", ""), issue.get("location", "")))
    print("\nRaw answers saved in %s" % args.out)


if __name__ == "__main__":
    main()
