using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Builds the app's local Supertonic-3 reader on iOS. Mobile.Shared cannot reference
/// sherpa-onnx on iOS (the native framework is linked by the app project, which is the one that
/// needs it), so an app that ships the neural engine registers one of these in
/// <c>RegisterAppPlatformServices</c> and <see cref="IOSPlatformServices"/> picks it up. An app
/// that registers none (an iOS app without the engine) reads with the system voice only.
/// </summary>
public delegate ISpeechReader IosNeuralReaderFactory();
