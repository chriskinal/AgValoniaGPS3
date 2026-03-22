// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;

namespace AgValoniaGPS.Models.Configuration;

public enum HotkeyAction
{
    AutoSteer,
    CycleLines,
    FieldMenu,
    Flag,
    ManualSection,
    AutoSection,
    SnapPivot,
    NudgeLeft,
    NudgeRight,
    VehicleSettings,
    SteerWizard,
    Section1,
    Section2,
    Section3,
    Section4,
    Section5,
    Section6,
    Section7,
    Section8
}

public class HotkeyConfig : ReactiveObject
{
    private Dictionary<HotkeyAction, string> _bindings;
    private Dictionary<string, HotkeyAction>? _reverseMap;

    public static readonly Dictionary<HotkeyAction, string> Defaults = new()
    {
        { HotkeyAction.AutoSteer, "A" },
        { HotkeyAction.CycleLines, "C" },
        { HotkeyAction.FieldMenu, "F" },
        { HotkeyAction.Flag, "G" },
        { HotkeyAction.ManualSection, "M" },
        { HotkeyAction.AutoSection, "N" },
        { HotkeyAction.SnapPivot, "P" },
        { HotkeyAction.NudgeLeft, "T" },
        { HotkeyAction.NudgeRight, "Y" },
        { HotkeyAction.VehicleSettings, "V" },
        { HotkeyAction.SteerWizard, "W" },
        { HotkeyAction.Section1, "1" },
        { HotkeyAction.Section2, "2" },
        { HotkeyAction.Section3, "3" },
        { HotkeyAction.Section4, "4" },
        { HotkeyAction.Section5, "5" },
        { HotkeyAction.Section6, "6" },
        { HotkeyAction.Section7, "7" },
        { HotkeyAction.Section8, "8" },
    };

    public HotkeyConfig()
    {
        _bindings = new Dictionary<HotkeyAction, string>(Defaults);
    }

    public IReadOnlyDictionary<HotkeyAction, string> Bindings => _bindings;

    public string GetKeyForAction(HotkeyAction action)
    {
        return _bindings.GetValueOrDefault(action, "");
    }

    public HotkeyAction? GetActionForKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        _reverseMap ??= BuildReverseMap();
        return _reverseMap.TryGetValue(key, out var action) ? action : null;
    }

    public void SetKeyForAction(HotkeyAction action, string key)
    {
        // Remove any existing binding for this key
        var existing = _bindings.FirstOrDefault(kvp =>
            string.Equals(kvp.Value, key, StringComparison.OrdinalIgnoreCase));
        if (existing.Value != null && existing.Key != action)
        {
            _bindings[existing.Key] = "";
        }

        _bindings[action] = key.ToUpperInvariant();
        _reverseMap = null;
        this.RaisePropertyChanged(nameof(Bindings));
    }

    public void ResetToDefaults()
    {
        _bindings = new Dictionary<HotkeyAction, string>(Defaults);
        _reverseMap = null;
        this.RaisePropertyChanged(nameof(Bindings));
    }

    public void LoadFromDictionary(Dictionary<string, string> dict)
    {
        foreach (var kvp in dict)
        {
            if (Enum.TryParse<HotkeyAction>(kvp.Key, ignoreCase: true, out var action))
            {
                _bindings[action] = kvp.Value.ToUpperInvariant();
            }
        }
        _reverseMap = null;
        this.RaisePropertyChanged(nameof(Bindings));
    }

    public Dictionary<string, string> ToDictionary()
    {
        var dict = new Dictionary<string, string>();
        foreach (var kvp in _bindings)
        {
            // camelCase the enum name
            var name = kvp.Key.ToString();
            var camelCase = char.ToLowerInvariant(name[0]) + name.Substring(1);
            dict[camelCase] = kvp.Value;
        }
        return dict;
    }

    private Dictionary<string, HotkeyAction> BuildReverseMap()
    {
        var map = new Dictionary<string, HotkeyAction>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _bindings)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                map[kvp.Value] = kvp.Key;
            }
        }
        return map;
    }
}
