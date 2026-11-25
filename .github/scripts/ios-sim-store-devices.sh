# Sourced (not run) by ios-sim-store-shots.sh and ios-sim-store-preboot.sh: the App Store simulator of each device label. Reads $APP (dino | rtst). macOS bash 3.2.

# One line per device: label | ';'-separated device-type name patterns, in order | sizes App Store Connect accepts for that slot (WxH, portrait).
device_specs() {
  echo 'iphone-6.5|iPhone 11 Pro Max;iPhone Xs Max;iPhone 14 Plus;iPhone 13 Pro Max;iPhone 12 Pro Max|1242x2688 1284x2778'
  if [ "$APP" = dino ]; then
    # Slice 433: Dino's iPad shots are landscape and its iPhone 6.9" shots are exactly the frame's size (tools/emulator/store-captions.ps1 does not stretch or crop to another).
    echo 'ipad-13|iPad Pro 13-inch \(M[0-9]\);iPad Air 13-inch \(M[0-9]\)|2752x2064'
    echo 'iphone-6.9|iPhone 17 Pro Max;iPhone 16 Pro Max|1320x2868'
  else
    echo 'ipad-13|iPad Pro 13-inch \(M[0-9]\);iPad Air 13-inch \(M[0-9]\)|2064x2752 2048x2732'
    echo 'iphone-6.9|iPhone 17 Pro Max;iPhone 16 Pro Max|1320x2868 1290x2796 1260x2736'
  fi
}

# The device-type name patterns of one label (the second field of its device_specs line); empty for an unknown label.
device_patterns() {
  device_specs | awk -F'|' -v want="$1" '$1 == want { print $2 }'
}
