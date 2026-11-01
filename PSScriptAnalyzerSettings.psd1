@{
    # Slice 225 item 17: PSScriptAnalyzer settings for tools/**/*.ps1, run in .github/workflows/tools-selftests.yml (the module is installed on demand there).
    # The BASELINE is the findings of 2026-11-01 (69 scripts: 884 information, 653 warnings, 1 error). The job fails on any Error-level finding; Warnings are listed, not failing.
    # Rules that fire all over the CLI scripts by design are switched off below, with their count that day; a rule that is not listed here is live.
    Severity     = @('Error', 'Warning', 'ParseError')
    ExcludeRules = @(
        'PSAvoidUsingWriteHost'                          # 345: these are console scripts, their output is the report
        'PSUseSingularNouns'                             # 102: names such as Get-LogLines read better in the plural
        'PSUseShouldProcessForStateChangingFunctions'    # 84: helpers, not cmdlets; -WhatIf is where a script needs one
        'PSReviewUnusedParameter'                        # 62: parameters kept for the dot-sourcing scripts, and splatted ones
        'PSUseUsingScopeModifierInNewRunspaces'          # 42: Start-Job script blocks take their values through -ArgumentList
        'PSUseBOMForUnicodeEncodedFile'                  # 2: the scripts are read by PowerShell 7 and Windows PowerShell with the same bytes today
    )
}
