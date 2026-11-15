using CesiumLoader.SDK.Manifests;

[assembly: ModManifest(
    "聊天敏感词绕过",
    "1.0.0",
    "TenkinMori",
    "聊天发送不再被敏感词替换；只影响聊天，改名与公会名/公告仍按原样校验。",
    Permissions = ModPermission.ReadGameState | ModPermission.ModifyGameState,
    SdkVersion = "3.1.1")]
