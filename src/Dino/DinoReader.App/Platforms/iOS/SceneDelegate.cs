using Foundation;

namespace DinoReader;

/// <summary>
/// The UIKit scene delegate (Slice 379 part B): apps built with the iOS 27 SDK must use the scene lifecycle (MT7199). MAUI forwards the scene
/// events to its cross-platform Window lifecycle itself, so nothing is added here. The registered name must equal the
/// <c>UISceneDelegateClassName</c> in <c>Info.plist</c>.
/// </summary>
[Register(SceneDelegate.RegisteredName)]
public class SceneDelegate : MauiUISceneDelegate
{
	public const string RegisteredName = "SceneDelegate";
}
