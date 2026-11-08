# check: Apps=dino EstimateSeconds=0 NeedsPack=false Variant=debug Blocked="retired in Slice 320: Dino has no Kokoro voice pack since Slice 278 (it plays pre-made audio packs), so Dino has no short-line engine to probe"
Write-Host 'short-lines is retired (Slice 320): Dino has no Kokoro voice pack, so there is nothing to probe. This is not a pass; the check did not run.'; exit 2
