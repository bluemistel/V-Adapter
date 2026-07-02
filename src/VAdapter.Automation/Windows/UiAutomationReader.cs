using System.Windows.Automation;
using System.Windows.Automation.Text;
using VAdapter.Core.Models;

namespace VAdapter.Automation.Windows;

/// <summary>
/// UI Automation で対象アプリの UI 要素テキストを読み取り、保存ダイアログのファイル名欄へ値を設定する。
/// VOICEROID2（WPF）等、Win32 メッセージや OCR では確実に取れない要素の取得に使う。
/// </summary>
public sealed class UiAutomationReader
{
    /// <summary>
    /// ルートウィンドウ配下から <paramref name="selector"/> に一致する要素のテキストを取得する。
    /// ValuePattern → TextPattern → Name の順で解決。取得不可なら null。
    /// </summary>
    public string? ReadText(IntPtr rootWindow, UiElementSelector selector)
    {
        try
        {
            if (rootWindow == IntPtr.Zero || !selector.HasAnyCondition)
                return null;

            var root = AutomationElement.FromHandle(rootWindow);
            var element = FindElement(root, selector);
            return element is null ? null : GetText(element);
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>スクリーン座標直下の UI 要素のテキストを取得する（実行時に都度 FromPoint で解決）。</summary>
    public string? ReadTextAtPoint(int x, int y)
    {
        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            return element is null ? null : GetText(element);
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>スクリーン座標直下の UI 要素からセレクタ（AutomationId/Name/ControlType）を採取する。</summary>
    public UiElementSelector? SelectorFromPoint(int x, int y)
    {
        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            if (element is null)
                return null;

            var info = element.Current;
            return new UiElementSelector
            {
                AutomationId = NullIfEmpty(info.AutomationId),
                Name = NullIfEmpty(info.Name),
                ControlType = ControlTypeName(info.ControlType),
            };
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// ルートウィンドウ（保存ダイアログ）配下のファイル名欄へ値を設定する。
    /// selector 指定があればそれを、無ければ最初の編集可能要素（Edit/ComboBox で ValuePattern 対応）を対象にする。
    /// </summary>
    public bool SetText(IntPtr rootWindow, UiElementSelector? selector, string value)
    {
        try
        {
            if (rootWindow == IntPtr.Zero)
                return false;

            var root = AutomationElement.FromHandle(rootWindow);
            var target = selector is { HasAnyCondition: true }
                ? FindElement(root, selector)
                : FindFileNameField(root);

            if (target is null)
                return false;

            if (target.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObj)
                && patternObj is ValuePattern vp && !vp.Current.IsReadOnly)
            {
                vp.SetValue(value);
                return true;
            }
            return false;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException
                                   or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            return false;
        }
    }

    // --- 内部 ---

    private static AutomationElement? FindElement(AutomationElement root, UiElementSelector selector)
    {
        var conditions = new List<Condition>();
        if (!string.IsNullOrEmpty(selector.AutomationId))
            conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, selector.AutomationId));
        if (!string.IsNullOrEmpty(selector.Name))
            conditions.Add(new PropertyCondition(AutomationElement.NameProperty, selector.Name));
        if (!string.IsNullOrEmpty(selector.ControlType) && MapControlType(selector.ControlType!) is { } ct)
            conditions.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, ct));

        if (conditions.Count == 0)
            return null;

        Condition condition = conditions.Count == 1 ? conditions[0] : new AndCondition(conditions.ToArray());
        return root.FindFirst(TreeScope.Descendants | TreeScope.Element, condition);
    }

    /// <summary>保存ダイアログの「ファイル名」欄を狙って特定する（誤って検索欄・アドレス欄を掴まないため）。</summary>
    private static AutomationElement? FindFileNameField(AutomationElement root)
    {
        // 1) AutomationId "1001"（Vista 以降のファイル名コンボ）。
        var byId = root.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "1001"));
        if (EditableOf(byId) is { } e1)
            return e1;

        // 2) Name に "ファイル名" を含む ComboBox/Edit。
        foreach (var ct in new[] { ControlType.ComboBox, ControlType.Edit })
        {
            var all = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ct));
            foreach (AutomationElement e in all)
            {
                var name = e.Current.Name;
                if (!string.IsNullOrEmpty(name) && name.Contains("ファイル名") && EditableOf(e) is { } ed)
                    return ed;
            }
        }

        // 3) フォールバック: 最初の編集可能要素。
        return FindFirstEditable(root);
    }

    private static AutomationElement? FindFirstEditable(AutomationElement root)
    {
        // Edit を優先、無ければ ComboBox。ValuePattern 対応・読み取り専用でない最初の要素。
        foreach (var ct in new[] { ControlType.Edit, ControlType.ComboBox })
        {
            var found = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ct));
            foreach (AutomationElement e in found)
            {
                if (EditableOf(e) is { } editable)
                    return editable;
            }
        }
        return null;
    }

    /// <summary>要素自身が ValuePattern 対応ならそれを、コンボ等なら配下の編集可能要素を返す。</summary>
    private static AutomationElement? EditableOf(AutomationElement? element)
    {
        if (element is null)
            return null;
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var p) && p is ValuePattern vp && !vp.Current.IsReadOnly)
            return element;

        var childEdit = element.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        if (childEdit is not null
            && childEdit.TryGetCurrentPattern(ValuePattern.Pattern, out var cp) && cp is ValuePattern cvp && !cvp.Current.IsReadOnly)
            return childEdit;
        return null;
    }

    private static string? GetText(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var vObj) && vObj is ValuePattern vp)
        {
            var v = vp.Current.Value;
            if (!string.IsNullOrEmpty(v))
                return v;
        }

        if (element.TryGetCurrentPattern(TextPattern.Pattern, out var tObj) && tObj is TextPattern tp)
        {
            var text = tp.DocumentRange.GetText(-1);
            if (!string.IsNullOrEmpty(text))
                return text.TrimEnd('\r', '\n');
        }

        // 選択コンテナ（リスト/タブ等）を掴んだ場合は、選択中の項目名を返す。
        // VOICEROID2 の左キャラクター一覧のように、コンテナ自体にテキストが無いケースに対応。
        var selName = TryGetSelectedName(element);
        if (!string.IsNullOrEmpty(selName))
            return selName;

        var name = element.Current.Name;
        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>要素が選択コンテナなら選択中項目の名前を、選択項目自身なら自分の名前を返す。</summary>
    private static string? TryGetSelectedName(AutomationElement element)
    {
        try
        {
            // 自身が選択項目（ListItem/TabItem 等）の場合。
            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var siObj)
                && siObj is SelectionItemPattern si && si.Current.IsSelected)
            {
                var n = element.Current.Name;
                if (!string.IsNullOrEmpty(n)) return n;
            }

            // 選択コンテナ（List/Tab 等）の場合は選択中の子の名前。
            if (element.TryGetCurrentPattern(SelectionPattern.Pattern, out var sObj) && sObj is SelectionPattern sp)
            {
                var selected = sp.Current.GetSelection();
                if (selected.Length > 0)
                {
                    var n = selected[0].Current.Name;
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }

            // 子孫に選択中の項目があれば、その名前。
            var selectedChild = element.FindFirst(TreeScope.Descendants,
                new PropertyCondition(SelectionItemPatternIdentifiers.IsSelectedProperty, true));
            var childName = selectedChild?.Current.Name;
            return string.IsNullOrEmpty(childName) ? null : childName;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static string? ControlTypeName(ControlType type)
    {
        // ProgrammaticName は "ControlType.Edit" 形式。末尾の名前部分のみ保持。
        var pn = type?.ProgrammaticName;
        if (string.IsNullOrEmpty(pn))
            return null;
        var dot = pn.LastIndexOf('.');
        return dot >= 0 && dot < pn.Length - 1 ? pn[(dot + 1)..] : pn;
    }

    private static ControlType? MapControlType(string name) => name.ToLowerInvariant() switch
    {
        "edit" => ControlType.Edit,
        "text" => ControlType.Text,
        "document" => ControlType.Document,
        "button" => ControlType.Button,
        "combobox" => ControlType.ComboBox,
        "list" => ControlType.List,
        "listitem" => ControlType.ListItem,
        "pane" => ControlType.Pane,
        "group" => ControlType.Group,
        "custom" => ControlType.Custom,
        _ => null,
    };

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
