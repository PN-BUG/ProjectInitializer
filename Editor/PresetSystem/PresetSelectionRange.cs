using System;
using System.Collections.Generic;

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
}
