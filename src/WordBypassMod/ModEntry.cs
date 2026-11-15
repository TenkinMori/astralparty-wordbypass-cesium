using System;
using System.Collections;
using System.Reflection;
using CesiumLoader.SDK.Configuration;
using CesiumLoader.SDK.Mods;
using Tools;
using UI;

namespace WordBypassMod
{
    public static class ModEntry
    {
        public static void Main()
        {
            ModBase.Run(new WordBypassController());
        }
    }

    /// <summary>
    /// 聊天敏感词绕过（CesiumLoader 版）。
    ///
    /// 移植自 AstralParty Next 的 NextWordBypass：原版用 BodyCall 把
    /// <c>Tools.DFAAlgorithm::StringCheckAndReplace(string)</c> 的整个方法体换成
    /// <c>return text;</c>，需要改 NextUi.dll 热更包。
    ///
    /// 本模组不改任何游戏文件，改为在运行时把 DFA 词表换成空表：
    /// <c>StringCheckAndReplace</c> 开头就是 <c>if (hashtable == null) return targetStr;</c>，
    /// 而空表（Init(0)，非 null、0 个词根）在 <c>SensitiveWordsLength</c> 里永远匹配不到，
    /// 所以同样原样返回 —— 对聊天发送来说与被换掉方法体等价。
    ///
    /// 词表只由聊天窗口（ChatWindow）和改名/公会窗口自己装卸，因此本模组只在
    /// <b>聊天窗口显示期间</b>把词表置空，窗口关闭后还原：改名、公会名、公会公告
    /// 仍然走原来的校验，范围与原版一致。
    /// </summary>
    public sealed class WordBypassController : ModBase
    {
        private const double PollSeconds = 0.1;

        /// <summary>DFA 的私有静态词表（Hashtable）。</summary>
        private static readonly FieldInfo TableField =
            typeof(DFAAlgorithm).GetField("hashtable", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>UIManager 里缓存的聊天窗口实例；为 null 表示从没开过聊天。</summary>
        private static readonly FieldInfo ChatWindowField =
            typeof(UIManager).GetField("_cachedChatWindow", BindingFlags.Instance | BindingFlags.NonPublic);

        private bool _enabled = true;
        private double _nextPoll;
        private bool _neutralized;
        private object _savedTable;
        private bool _warnedLayout;

        public override string Name { get { return "聊天敏感词绕过"; } }
        public override string Version { get { return "1.0.0"; } }
        public override string Author { get { return "TenkinMori"; } }
        public override string Description
        {
            get { return "聊天发送不再被敏感词替换；只影响聊天，改名与公会名/公告仍按原样校验。"; }
        }

        public override void OnInitialize()
        {
            ReadConfig(true);
            Log.Info("配置: enabled=" + _enabled
                + "，词表字段=" + (TableField != null)
                + "，聊天窗口字段=" + (ChatWindowField != null));
            if (TableField == null || ChatWindowField == null) WarnLayout();
        }

        public override void OnConfigChanged()
        {
            ReadConfig(false);
            if (!_enabled) Restore();
            Log.Info("配置已更新: enabled=" + _enabled);
        }

        public override void OnUnload()
        {
            Restore();
        }

        public override void OnUpdate()
        {
            double now = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            if (now < _nextPoll) return;
            _nextPoll = now + PollSeconds;

            try { Tick(); }
            catch (Exception e) { Log.Warn("巡检失败: " + e.Message); }
        }

        private void Tick()
        {
            if (!_enabled) return;
            if (TableField == null || ChatWindowField == null) return;

            if (!IsChatVisible())
            {
                Restore();
                return;
            }

            object table = TableField.GetValue(null);
            IDictionary dict = table as IDictionary;
            if (dict == null || dict.Count == 0) return;   // 词表还没装，或者已经是空表

            _savedTable = table;
            DFAAlgorithm.Init(0);                          // 空词表 ⇒ 聊天文本原样返回
            _neutralized = true;
            Log.Info("聊天窗口已打开：词表已置空（" + dict.Count + " 个首字符）");
        }

        /// <summary>恢复到置空前那张词表；游戏自己清掉或重新装过表时不动它。</summary>
        private void Restore()
        {
            if (!_neutralized) return;
            _neutralized = false;

            object saved = _savedTable;
            _savedTable = null;
            if (saved == null || TableField == null) return;

            object table = TableField.GetValue(null);
            if (table == null) return;                     // 聊天窗口 OnHide 已 UnloadSensitiveWords

            IDictionary dict = table as IDictionary;
            if (dict == null || dict.Count != 0) return;   // 游戏重新装了表，别覆盖

            TableField.SetValue(null, saved);
            Log.Info("聊天窗口已关闭：词表已还原");
        }

        private bool IsChatVisible()
        {
            if (ChatWindowField == null) return false;
            if (!SimpleSingletonProvider<UIManager>.hasInstance) return false;

            // 不构造窗口：只读 UIManager 已经缓存的那一个；从没开过聊天就是 null。
            ChatWindow window = ChatWindowField.GetValue(SimpleSingletonProvider<UIManager>.inst) as ChatWindow;
            if (window == null) return false;

            // FairyGUI.Window.isShowing：Show() 后为 true、Hide() 后为 false。
            return window.isShowing;
        }

        private void ReadConfig(bool writeDefaults)
        {
            ModConfig cfg = Config;
            if (cfg == null) return;

            if (writeDefaults && cfg.Count == 0)
            {
                cfg.Set("enabled", true);
                cfg.SaveIfDirty();
            }

            _enabled = cfg.GetBool("enabled", true);
        }

        private void WarnLayout()
        {
            if (_warnedLayout) return;
            _warnedLayout = true;
            Log.Warn("游戏程序集布局变了（找不到 DFAAlgorithm.hashtable 或 UIManager._cachedChatWindow），"
                + "本模组不会生效；需要按新版本重新确认字段名。");
        }
    }
}
