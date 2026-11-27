// Fixture source for test-lint-flows.ps1: a marker logged by a Swift file.
func logIt() { print("StoreKit swift marker ok") }
func logEscapes() { print("swift \"quoted\" marker"); print("swift \'single\' marker"); print(#"swift raw "quoted" marker"#) }   // Slice 406 item 3d: Swift escapes
