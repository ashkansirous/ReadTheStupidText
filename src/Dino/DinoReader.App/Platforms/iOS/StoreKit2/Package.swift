// swift-tools-version:5.9
// Slice 390: Dino Reader's own thin wrapper over StoreKit 2 (Swift-only, so .NET for iOS cannot bind it). Built into
// DinoStoreKit.xcframework by build-xcframework.sh (a macOS job; Windows cannot build it) and called from
// IosPurchases.cs through C functions (@_cdecl), one JSON string in and one out, so no Objective-C binding project is needed.
import PackageDescription

let package = Package(
    name: "DinoStoreKit",
    platforms: [.iOS("15.5")],
    products: [
        .library(name: "DinoStoreKit", type: .dynamic, targets: ["DinoStoreKit"]),
    ],
    targets: [
        .target(name: "DinoStoreKit"),
    ],
    swiftLanguageVersions: [.v5]
)
