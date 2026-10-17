using System;
using System.Collections.Generic;

namespace Anseong.Core
{
    /// <summary>
    /// 임포트된 재질 이름을 공용 재질 슬롯 이름(ArmorMain 등)과 맞춘다.
    /// Blender가 붙이는 중복 접미사(".001")는 무시하고, 대소문자도 구분하지 않는다.
    /// </summary>
    public static class MaterialSlotNames
    {
        /// <summary>끝의 ".숫자" 접미사를 뗀 이름. "ArmorMain.001" → "ArmorMain".</summary>
        public static string BaseName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return string.Empty;
            var name = materialName.Trim();
            var dot = name.LastIndexOf('.');
            if (dot > 0 && dot < name.Length - 1)
            {
                var allDigits = true;
                for (var i = dot + 1; i < name.Length; i++)
                    if (!char.IsDigit(name[i])) { allDigits = false; break; }
                if (allDigits) name = name.Substring(0, dot);
            }
            return name;
        }

        /// <summary>materialName에 맞는 슬롯의 인덱스. 없으면 -1.</summary>
        public static int IndexOf(string materialName, IReadOnlyList<string> slotNames)
        {
            if (slotNames == null) return -1;
            var baseName = BaseName(materialName);
            if (baseName.Length == 0) return -1;
            for (var i = 0; i < slotNames.Count; i++)
            {
                var slot = slotNames[i] == null ? string.Empty : slotNames[i].Trim();
                if (slot.Length > 0 && string.Equals(baseName, slot, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
    }
}
