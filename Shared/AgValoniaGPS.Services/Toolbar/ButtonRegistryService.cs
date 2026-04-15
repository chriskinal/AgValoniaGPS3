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

using System.Collections.Generic;
using System.Linq;
using AgValoniaGPS.Models.Toolbar;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Toolbar;

public class ButtonRegistryService : IButtonRegistryService
{
    private const string Icons = "avares://AgValoniaGPS.Views/Assets/Icons/";

    private readonly Dictionary<string, ButtonDefinition> _buttons;

    public ButtonRegistryService()
    {
        var definitions = CreateDefinitions();
        _buttons = definitions.ToDictionary(b => b.Id);
    }

    public ButtonDefinition? GetById(string id) =>
        _buttons.GetValueOrDefault(id);

    public IReadOnlyList<ButtonDefinition> GetAll() =>
        _buttons.Values.ToList();

    public IReadOnlyList<ButtonDefinition> GetByCategory(ButtonCategory category) =>
        _buttons.Values.Where(b => b.Category == category).ToList();

    private static List<ButtonDefinition> CreateDefinitions() =>
    [
        // === Guidance buttons (from RightNavigationPanel) ===

        new ButtonDefinition
        {
            Id = "ToggleContourMode",
            Tooltip = "Contour Mode On/Off",
            IconResource = $"{Icons}ContourOff.png",
            ActiveIconResource = $"{Icons}ContourOn.png",
            CommandPropertyName = "ToggleContourModeCommand",
            IsActivePropertyName = "IsContourModeOn",
            Category = ButtonCategory.Guidance
        },
        new ButtonDefinition
        {
            Id = "ToggleManualMode",
            Tooltip = "All Sections Manual On/Off",
            IconResource = $"{Icons}ManualOff.png",
            ActiveIconResource = $"{Icons}ManualOn.png",
            CommandPropertyName = "ToggleManualModeCommand",
            IsActivePropertyName = "IsManualSectionMode",
            Category = ButtonCategory.Guidance
        },
        new ButtonDefinition
        {
            Id = "ToggleSectionMaster",
            Tooltip = "All Sections Auto/Off",
            IconResource = $"{Icons}SectionMasterOff.png",
            ActiveIconResource = $"{Icons}SectionMasterOn.png",
            CommandPropertyName = "ToggleSectionMasterCommand",
            IsActivePropertyName = "IsSectionMasterOn",
            Category = ButtonCategory.Sections
        },
        new ButtonDefinition
        {
            Id = "ToggleYouTurn",
            Tooltip = "YouTurn Auto U-Turn",
            IconResource = $"{Icons}YouTurnNo.png",
            ActiveIconResource = $"{Icons}YouTurnYes.png",
            CommandPropertyName = "ToggleYouTurnCommand",
            IsActivePropertyName = "IsYouTurnEnabled",
            IsVisiblePropertyName = "IsUTurnButtonVisible",
            Category = ButtonCategory.Guidance
        },
        new ButtonDefinition
        {
            Id = "ManualYouTurnLeft",
            Tooltip = "Manual U-Turn Left",
            IconResource = $"{Icons}YouTurnNo.png",
            CommandPropertyName = "ManualYouTurnLeftCommand",
            IsVisiblePropertyName = "IsAutoSteerEngaged",
            Category = ButtonCategory.Guidance
        },
        new ButtonDefinition
        {
            Id = "ManualYouTurnRight",
            Tooltip = "Manual U-Turn Right",
            IconResource = $"{Icons}YouTurnNo.png",
            CommandPropertyName = "ManualYouTurnRightCommand",
            IsVisiblePropertyName = "IsAutoSteerEngaged",
            Category = ButtonCategory.Guidance
        },
        new ButtonDefinition
        {
            Id = "ToggleAutoSteer",
            Tooltip = "AutoSteer",
            IconResource = $"{Icons}AutoSteerOff.png",
            ActiveIconResource = $"{Icons}AutoSteerOn.png",
            CommandPropertyName = "ToggleAutoSteerCommand",
            IsActivePropertyName = "IsAutoSteerEngaged",
            Category = ButtonCategory.Guidance
        },

        // === Section buttons ===

        new ButtonDefinition
        {
            Id = "ToggleSectionOverlay",
            Tooltip = "Section Control",
            IconResource = $"{Icons}SectionMasterOff.png",
            CommandPropertyName = "ToggleSectionOverlayCommand",
            Category = ButtonCategory.Sections
        },
        new ButtonDefinition
        {
            Id = "ToggleSectionInHeadland",
            Tooltip = "Section Control in Headland",
            IconResource = $"{Icons}HeadlandSectionOff.png",
            ActiveIconResource = $"{Icons}HeadlandSectionOn.png",
            CommandPropertyName = "ToggleSectionInHeadlandCommand",
            IsActivePropertyName = "IsSectionControlInHeadland",
            Category = ButtonCategory.Sections
        },
        new ButtonDefinition
        {
            Id = "ChangeMappingColor",
            Tooltip = "Section Mapping Color",
            IconResource = $"{Icons}SectionMapping.png",
            CommandPropertyName = "ChangeMappingColorCommand",
            Category = ButtonCategory.Sections
        },

        // === Track buttons (from BottomNavigationPanel) ===

        new ButtonDefinition
        {
            Id = "ToggleHeadland",
            Tooltip = "Toggle Headland",
            IconResource = $"{Icons}HeadlandOff.png",
            ActiveIconResource = $"{Icons}HeadlandOn.png",
            CommandPropertyName = "ToggleHeadlandCommand",
            IsActivePropertyName = "IsHeadlandOn",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "SnapLeft",
            Tooltip = "Snap to Left Track",
            IconResource = $"{Icons}SnapLeft.png",
            CommandPropertyName = "SnapLeftCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "SnapRight",
            Tooltip = "Snap to Right Track",
            IconResource = $"{Icons}SnapRight.png",
            CommandPropertyName = "SnapRightCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "SnapToPivot",
            Tooltip = "Snap to Pivot",
            IconResource = $"{Icons}SnapToPivot.png",
            CommandPropertyName = "SnapToPivotCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "ResetToolHeading",
            Tooltip = "Reset Tool Heading",
            IconResource = $"{Icons}ResetTool.png",
            CommandPropertyName = "ResetToolHeadingCommand",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "ToggleTramDisplay",
            Tooltip = "Tram Lines Display Mode",
            IconResource = $"{Icons}TramAll.png",
            CommandPropertyName = "ToggleTramDisplayCommand",
            IsVisiblePropertyName = "IsFieldOpen",
            Category = ButtonCategory.View
        },
        new ButtonDefinition
        {
            Id = "CycleUTurnSkipRows",
            Tooltip = "U-Turn Skip Rows (tap to cycle 0-9)",
            IconResource = $"{Icons}YouSkipOff.png",
            CommandPropertyName = "CycleUTurnSkipRowsCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Guidance
        },
        new ButtonDefinition
        {
            Id = "ToggleUTurnSkipRows",
            Tooltip = "U-Turn Skip Rows On/Off",
            IconResource = $"{Icons}YouSkipOff.png",
            ActiveIconResource = $"{Icons}YouSkipOn.png",
            CommandPropertyName = "ToggleUTurnSkipRowsCommand",
            IsActivePropertyName = "IsUTurnSkipRowsEnabled",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Guidance
        },

        // === AB Line / Track management buttons (from BottomNavigationPanel AB Line flyout) ===

        new ButtonDefinition
        {
            Id = "ShowTracksDialog",
            Tooltip = "Tracks - Manage AB Lines",
            IconResource = $"{Icons}ABTracks.png",
            CommandPropertyName = "ShowTracksDialogCommand",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "ToggleAutoTrack",
            Tooltip = "Auto Track Select (closest track)",
            IconResource = $"{Icons}ABTracks.png",
            CommandPropertyName = "ToggleAutoTrackCommand",
            IsActivePropertyName = "IsAutoTrackEnabled",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "ShowQuickABSelector",
            Tooltip = "Quick AB Line Creator",
            IconResource = $"{Icons}ABTrackAPlus.png",
            CommandPropertyName = "ShowQuickABSelectorCommand",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "CreateTrackFromBoundary",
            Tooltip = "Create AB Line from Boundary Edge",
            IconResource = $"{Icons}ABTracks.png",
            CommandPropertyName = "CreateTrackFromBoundaryCommand",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "ShowDrawABDialog",
            Tooltip = "Draw AB Line on Map",
            IconResource = $"{Icons}ABDraw.png",
            CommandPropertyName = "ShowDrawABDialogCommand",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "CycleABLines",
            Tooltip = "Cycle AB Lines",
            IconResource = $"{Icons}ABLineCycle.png",
            CommandPropertyName = "CycleABLinesCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "SmoothABLine",
            Tooltip = "Smooth AB Curve",
            IconResource = $"{Icons}ABSmooth.png",
            CommandPropertyName = "SmoothABLineCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "DeleteContours",
            Tooltip = "Delete Contours",
            IconResource = $"{Icons}TrashContourRef.png",
            CommandPropertyName = "DeleteContoursCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },

        // === Nudge controls ===

        new ButtonDefinition
        {
            Id = "NudgeLeft",
            Tooltip = "Nudge Left (A+)",
            IconResource = $"{Icons}APlusPlusA.png",
            CommandPropertyName = "NudgeLeftCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "NudgeRight",
            Tooltip = "Nudge Right (B+)",
            IconResource = $"{Icons}APlusPlusB.png",
            CommandPropertyName = "NudgeRightCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "FineNudgeLeft",
            Tooltip = "Fine Nudge Left (A-)",
            IconResource = $"{Icons}APlusMinusA.png",
            CommandPropertyName = "FineNudgeLeftCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "FineNudgeRight",
            Tooltip = "Fine Nudge Right (B-)",
            IconResource = $"{Icons}APlusMinusB.png",
            CommandPropertyName = "FineNudgeRightCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "HalfToolNudgeLeft",
            Tooltip = "Nudge Left Half Tool Width",
            IconResource = $"{Icons}APlusPlusA.png",
            CommandPropertyName = "HalfToolNudgeLeftCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "HalfToolNudgeRight",
            Tooltip = "Nudge Right Half Tool Width",
            IconResource = $"{Icons}APlusPlusB.png",
            CommandPropertyName = "HalfToolNudgeRightCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },
        new ButtonDefinition
        {
            Id = "ResetNudge",
            Tooltip = "Reset Nudge",
            IconResource = $"{Icons}ResetTool.png",
            CommandPropertyName = "ResetNudgeCommand",
            IsVisiblePropertyName = "HasActiveTrack",
            Category = ButtonCategory.Track
        },

        // === Flags (from BottomNavigationPanel Flags flyout) ===

        new ButtonDefinition
        {
            Id = "PlaceFlagHere",
            Tooltip = "Place flag at current position",
            IconResource = $"{Icons}FlagRed.png",
            CommandPropertyName = "PlaceFlagHereCommand",
            Category = ButtonCategory.Flags
        },
        new ButtonDefinition
        {
            Id = "PlaceFlagOnClick",
            Tooltip = "Tap map to place flag",
            IconResource = $"{Icons}FlagGrn.png",
            CommandPropertyName = "PlaceFlagOnClickCommand",
            Category = ButtonCategory.Flags
        },
        new ButtonDefinition
        {
            Id = "ShowFlagList",
            Tooltip = "View and manage all flags",
            IconResource = $"{Icons}FlagYel.png",
            CommandPropertyName = "ShowFlagListCommand",
            Category = ButtonCategory.Flags
        },
    ];
}
