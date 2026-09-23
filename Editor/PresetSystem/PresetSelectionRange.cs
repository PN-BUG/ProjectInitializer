using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectInitializer
{
    internal static class PresetSelectionRange
    {
        // Shift 点击沿用上一次普通点击的锚点，按界面显示顺序设置整段条目。
        public static void Apply<T>(IList<T> items, int index, bool value, bool shift, ref int anchor,
            Action<T, bool> setValue)
        {
            if (items == null || index < 0 || index >= items.Count) return;
            if (shift && anchor >= 0 && anchor < items.Count)
            {
                int start = Math.Min(anchor, index);
                int end = Math.Max(anchor, index);
                for (int i = start; i <= end; i++)
                    setValue(items[i], value);
            }
            else
            {
                setValue(items[index], value);
                anchor = index;
            }
        }
    }

    internal sealed class PresetRangeToggle
    {
        private int _anchor = -1;
        private int _mouseDownIndex = -1;
        private bool _mouseDownShift;

        public void Reset()
        {
            _anchor = -1;
            _mouseDownIndex = -1;
            _mouseDownShift = false;
        }

        public bool Draw<T>(IList<T> items, int index, bool currentValue, float width, Action<T, bool> setValue)
        {
            Rect rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, GUILayout.Width(width));
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rect.Contains(currentEvent.mousePosition))
            {
                _mouseDownIndex = index;
                _mouseDownShift = currentEvent.shift;
            }

            // 包和设置列表会提高 indentLevel；复选框已由布局定位，不能再缩进一次。
            int originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            bool nextValue;
            try { nextValue = EditorGUI.Toggle(rect, currentValue); }
            finally { EditorGUI.indentLevel = originalIndent; }
            bool changed = nextValue != currentValue;
            if (changed)
            {
                bool shift = _mouseDownIndex == index ? _mouseDownShift : currentEvent.shift;
                PresetSelectionRange.Apply(items, index, nextValue, shift, ref _anchor, setValue);
                _mouseDownIndex = -1;
            }
            else if (currentEvent.rawType == EventType.MouseUp && _mouseDownIndex == index)
            {
                _mouseDownIndex = -1;
            }
            return changed;
        }
    }

    internal sealed class PresetMultiSelection<T> where T : class
    {
        private readonly HashSet<T> _selected = new HashSet<T>();
        private int _anchor = -1;

        public int Count => _selected.Count;
        public bool Contains(T item) => item != null && _selected.Contains(item);

        public void Clear()
        {
            _selected.Clear();
            _anchor = -1;
        }

        public void Prune(IList<T> visibleItems)
        {
            _selected.IntersectWith(visibleItems);
            if (_anchor >= visibleItems.Count) _anchor = -1;
        }

        public void Click(IList<T> visibleItems, int index, bool shift, bool additive)
        {
            if (index < 0 || index >= visibleItems.Count || visibleItems[index] == null) return;
            if (shift && _anchor >= 0 && _anchor < visibleItems.Count)
            {
                if (!additive) _selected.Clear();
                int start = Math.Min(_anchor, index);
                int end = Math.Max(_anchor, index);
                for (int i = start; i <= end; i++)
                    if (visibleItems[i] != null) _selected.Add(visibleItems[i]);
                return;
            }

            T item = visibleItems[index];
            if (additive)
            {
                if (!_selected.Add(item)) _selected.Remove(item);
            }
            else
            {
                _selected.Clear();
                _selected.Add(item);
            }
            _anchor = index;
        }

        public void Apply(bool value, Action<T, bool> setValue)
        {
            foreach (T item in _selected)
                setValue(item, value);
        }
    }

    internal static class PresetDirectoryRow
    {
        private static GUIStyle _labelStyle;

        public static bool Draw(string label, bool selected, bool dimmed = false)
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(5, 3, 0, 0)
                };
            }

            Rect rect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight + 2f,
                GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                if (selected)
                    EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                        ? new Color(0.20f, 0.39f, 0.59f, 0.65f)
                        : new Color(0.43f, 0.67f, 0.91f, 0.55f));
            }
            Color originalContentColor = GUI.contentColor;
            if (dimmed)
                GUI.contentColor = new Color(originalContentColor.r, originalContentColor.g,
                    originalContentColor.b, originalContentColor.a * 0.45f);
            try { return GUI.Toggle(rect, selected, label, _labelStyle) != selected; }
            finally { GUI.contentColor = originalContentColor; }
        }
    }

    internal static class PresetDirectoryGroupMenu
    {
        public static void Draw(IList<DirectoryEntry> entries, Action onChange)
        {
            if (!GUILayout.Button(new GUIContent("⋯", "此目录及子目录的全选/全不选"),
                    EditorStyles.miniButton, GUILayout.Width(26f))) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("全选此目录"), false, () =>
            {
                foreach (DirectoryEntry entry in entries) entry.enabled = true;
                onChange();
            });
            menu.AddItem(new GUIContent("全不选此目录"), false, () =>
            {
                foreach (DirectoryEntry entry in entries) entry.enabled = false;
                onChange();
            });
            menu.ShowAsContext();
        }
    }
}
