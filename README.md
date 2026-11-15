# 聊天敏感词绕过（CesiumLoader 版）

<p align="left">
  <img src="icon.png" width="96" alt="icon">
</p>

> **游戏：吉星派对（Astral Party）**
> **插件名：聊天敏感词绕过** ｜ **作者：TenkinMori**
>
> CesiumLoader 托管模组：把 `WordBypassMod.dll` 放进
> `AstralParty_ModLoader/mods/WordBypassMod/` 即可。

## 作用

**聊天发送不再被敏感词校验替换成 `***`。**

- 只影响聊天发送（好友聊天、公会聊天），改名 / 公会名 / 公会公告仍然照旧拦
- 只改本机内存里的词表，不改热更包、不改游戏文件、不碰服务器
- 关闭模组或关掉 `enabled` 立刻恢复原样

## 安装

前提：游戏已安装 CesiumLoader（根目录有 `version.dll` 和 `AstralParty_ModLoader/`）。

1. 关闭游戏
2. 把 `dist/WordBypassMod/` 整个文件夹复制到：

   ```text
   游戏目录/AstralParty_ModLoader/mods/WordBypassMod/
   ├─ WordBypassMod.dll
   ├─ WordBypassMod.json
   └─ icon.png
   ```

3. 重启游戏

卸载：删除 `mods/WordBypassMod/` 文件夹，重启游戏。

## 使用

进游戏后打开聊天窗口（好友 / 公会），正常打字发送即可，敏感词不再被替换成 `***`。

- 聊天窗口关掉后模组会把词表还原，改名 / 公会名 / 公会公告的校验保持原样
- 模组每 0.1 秒巡检一次聊天窗口；聊天窗口不在显示状态时不做任何改动

### 配置 `config.json`

首次启动后生成 `AstralParty_ModLoader/mods/WordBypassMod/config.json`：

```json
{
  "enabled": true
}
```

| 键 | 默认 | 说明 |
|---|---|---|
| `enabled` | `true` | 关掉后立刻还原词表，聊天恢复原版过滤 |

保存后 SDK 会热重载配置（CesiumLoader 2.6.0+），不需要重启游戏。

## 更新日志

### v1.0.0

- 首个版本：把 `AstralParty Next` 的 `NextWordBypass` 移植为 CesiumLoader 托管模组；
  聊天窗口打开期间把 DFA 词表换成空表，关闭时还原
- `UIManager._cachedChatWindow` 字段改名时自动退回公开属性 `UIManager.chat`

## 原理

### 移植对照

原版在 `AstralParty Next` 里是两个文件：

| 文件 | 作用 |
|---|---|
| `tools/modsrc_extra/NextWordBypass.cs` | Callee：`public static string Same(string text) => text;` |
| `client/spec/extra_sensitive_bypass.json` | BodyCall：把 `Tools.DFAAlgorithm::StringCheckAndReplace(string)` 的整个方法体换成 `ldarg.0 ; call NextBypass.NextWordBypass::Same ; ret` |

它必须用 `tools/build_bypass.py` 重新编译 `NextUi.dll` 热更包才能生效。

本仓库把它改成 **CesiumLoader 托管模组**，不再改任何 DLL：

| | NextWordBypass（原版） | 本模组 |
|---|---|---|
| 生效方式 | 改 `NextUi.dll` 热更包的方法体 | 运行时把 DFA 词表换成空表 |
| 安装 | 换客户端热更包 | 丢一个 DLL 到 `mods/` |
| 游戏更新 | 要重新打补丁包 | 通常不用动（字段不变即可） |
| 范围 | 只影响聊天 | 只影响聊天 |

### 为什么换表等价于换方法体

聊天发送的调用链（`UI.ChatWindow.SendMsg` / `SendGuildInputAsync`）：

```text
SensitiveWords.DealSensitiveWord(text)          // Core.SensitiveWords
  → text.DealSensitiveWord()                    // StaticConfigure 的扩展方法
      if (!DFAAlgorithm.IsValid()) return text;
      return DFAAlgorithm.StringCheckAndReplace(text);
```

而 `StringCheckAndReplace` 的第一句就是：

```csharp
if (hashtable == null) return targetStr;        // 没装词表 ⇒ 原样返回
```

词表是 `Tools.DFAAlgorithm` 的私有静态 `Hashtable`。`DFAAlgorithm.Init(0)` 会把它换成
**非 null、0 个词根的空表** —— `SensitiveWordsLength` 一个字符都匹配不到，
所以 `StringCheckAndReplace` 同样原样返回，聊天文本不会出现 `***`。
换句话说：原版是把「替换」这一步换成 `return text;`，本模组是让「替换」这一步无事可做，
对聊天发送的结果完全一致。

### 为什么只在聊天窗口打开时换表

词表不是常驻的：聊天窗口显示时 `ChatWindow.TryShowAsync()` 调
`StaticConfigure.LoadSensitiveWords()` 装表，`OnHide()` 调 `UnloadSensitiveWords()` 清表；
改名窗口（`PlayerRenameWindow`）、公会创建 / 公告 / 设置窗口也会各自装卸同一张表，
用来跑 `SensitiveWords.Valid(...)`。

所以模组每 0.1 秒巡检一次：

- 聊天窗口**显示中** → 词表非空就换成空表（聊天发送不过滤）；
- 聊天窗口**已关闭** → 如果还是我们那张空表就还原成原来那张。

这样改名、公会名、公会公告的校验完全不受影响，和原版「一个兄弟方法都不动」的口径一致。
判断窗口显示用的是 `UIManager` 里缓存的 `ChatWindow` 实例 + FairyGUI 的
`Window.isShowing`（只反射读私有字段 `_cachedChatWindow`，不构造窗口）。

### 为什么不用 Harmony

游戏是 **IL2CPP + HybridCLR** 构建，`AstralParty.Runtime` 是解释执行的热更程序集，
没有 MonoMod / Harmony 能挂的托管方法体入口；CesiumLoader SDK 也没有提供托管方法替换接口。
所以这里走 SDK 允许的路线：只读反射 + 调用游戏自己的公开静态方法（`DFAAlgorithm.Init`），
和本工作区其它 CesiumLoader 模组（RPC 实例替换、字段改写）是同一类做法。

## 验证

看 `AstralParty_ModLoader/logs/activity-mod-*.log`，出现下面几行即生效：

```text
[WordBypassMod] 配置: enabled=True，词表字段=True，聊天窗口字段=True
[WordBypassMod] 聊天窗口已打开：词表已置空（N 个首字符）
[WordBypassMod] 聊天窗口已关闭：词表已还原
```

同时 `AstralParty_ModLoader/logs/cesium-loader-*.log` 里模组数应该包含 `WordBypassMod`。

如果出现 `配置: ... 词表字段=False`，说明本版游戏把 `Tools.DFAAlgorithm.hashtable`
改了名，模组会写一条警告并停用，需要按新程序集重新确认字段名。

## 构建

需要 .NET SDK 8+：

```powershell
pwsh -File build.ps1
```

产物在 `dist/WordBypassMod/`，同时会打出 `dist/WordBypassMod-1.0.0.zip`。

- `lib/CesiumLoader.SDK.dll`：CesiumLoader SDK（MIT，随仓库提供以便直接构建）
- `refs/game/*.dll`：游戏热更程序集（2026-11-12 国服版本，`AstralParty.Runtime.dll`
  SHA256 `68B848C0…CDE4`），只用于编译引用；游戏更新后可用 CesiumLoader 仓库的
  `tools/fetch-game-refs.ps1` 重新提取

## 本版核对过的事实（2026-11-12 热更程序集）

- `Tools.DFAAlgorithm` 仍是 `public static class`，私有字段 `hashtable` 与
  `Init/IsValid/Clear/AddFilterWord/StringCheckAndReplace/Valid/SensitiveWordsLength` 都在。
- `StringCheckAndReplace` 在词表为空表时原样返回；空表下 `Valid` 也恒为 true（所以才需要按窗口还原）。
- `DealSensitiveWord` 全程序集只有 **聊天窗口**两处调用（好友发送、公会发送）；
  改名 / 公会三处走的是 `SensitiveWords.Valid`。
- 词表由 `ChatWindow.TryShowAsync()` 装载、`OnHide()` 卸载；改名与公会窗口各自装卸。
- `UI.ChatWindow` 仍是 `UIManager` 的 `chat` 属性；`_cachedChatWindow` 为私有字段。

离线用小样例词表跑过状态机：聊天开 → 文本原样；聊天关 → 游戏清表；改名窗口装表时不动；
常驻词表场景下关闭聊天会还原。

## 兼容性

- CesiumLoader SDK 3.1.1+（模组清单声明的下限；实测 3.8.1）
- 游戏版本 3.3.0（2026-11-12 热更后）
- PC 端 Windows x64（本仓库只发布 PC 版）

## 已知边界

- **纯客户端**：只改本机发送前的文本；服务器如果另有过滤，仍以服务器为准。
- **识别不到关键成员就不生效**：游戏若把 `DFAAlgorithm.hashtable` 改名，模组会在日志里写一条警告并停用，不会乱改别的状态；
  `UIManager._cachedChatWindow` 改名时会退回公开属性 `UIManager.chat`（会按需构造聊天窗口）。
- **罕见重叠**：聊天窗口开着的同时去操作公会名 / 公告（正常 UI 流程里基本不会同时打开），
  此时词表仍是空表，那一次校验会跳过。
- 游戏热更换了程序集后，若要继续跟随新程序集，建议按 `refs/` 重新编译。

## 许可

- 本模组代码：MIT（见 `LICENSE`）
- `lib/CesiumLoader.SDK.dll`：CesiumLoader SDK，MIT，版权归其作者
- `refs/game/` 下的游戏程序集版权归游戏官方，仅用于编译，不随本模组运行
