// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AgValoniaGPS.VehicleSimulator.Views;

/// <summary>
/// Top-down vehicle visualization showing body, rear wheels (fixed),
/// and front wheels (steered by WAS angle).
/// </summary>
public class VehicleTopView : Control
{
    public static readonly StyledProperty<double> WheelAngleProperty =
        AvaloniaProperty.Register<VehicleTopView, double>(nameof(WheelAngle));

    public static readonly StyledProperty<double> CommandedAngleProperty =
        AvaloniaProperty.Register<VehicleTopView, double>(nameof(CommandedAngle));

    public static readonly StyledProperty<bool> AutoSteerEngagedProperty =
        AvaloniaProperty.Register<VehicleTopView, bool>(nameof(AutoSteerEngaged));

    public double WheelAngle
    {
        get => GetValue(WheelAngleProperty);
        set => SetValue(WheelAngleProperty, value);
    }

    public double CommandedAngle
    {
        get => GetValue(CommandedAngleProperty);
        set => SetValue(CommandedAngleProperty, value);
    }

    public bool AutoSteerEngaged
    {
        get => GetValue(AutoSteerEngagedProperty);
        set => SetValue(AutoSteerEngagedProperty, value);
    }

    static VehicleTopView()
    {
        AffectsRender<VehicleTopView>(WheelAngleProperty, CommandedAngleProperty, AutoSteerEngagedProperty);
    }

    // Colors
    private static readonly IBrush BodyBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80));
    private static readonly IBrush WheelBrush = new SolidColorBrush(Color.FromRgb(40, 40, 40));
    private static readonly IBrush CommandedBrush = new SolidColorBrush(Color.FromArgb(100, 0, 180, 0));
    private static readonly IPen BodyPen = new Pen(Brushes.Gray, 1);
    private static readonly IPen WheelPen = new Pen(Brushes.Black, 1);
    private static readonly IPen CommandedPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 180, 0)), 1.5);
    private static readonly IPen CenterPen = new Pen(Brushes.Yellow, 1, DashStyle.Dash);

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w < 10 || h < 10) return;

        // Vehicle dimensions relative to control size
        double bodyW = w * 0.3;
        double bodyH = h * 0.7;
        double wheelW = bodyW * 0.2;
        double wheelH = bodyW * 0.55;
        double axleOffset = bodyW * 0.65; // wheel center offset from body center
        double frontAxleY = (h - bodyH) / 2 + bodyH * 0.15; // front axle position
        double rearAxleY = (h - bodyH) / 2 + bodyH * 0.85;  // rear axle position

        double cx = w / 2;
        double bodyTop = (h - bodyH) / 2;

        // Draw body
        var bodyRect = new Rect(cx - bodyW / 2, bodyTop, bodyW, bodyH);
        context.DrawRectangle(BodyBrush, BodyPen, bodyRect, 4, 4);

        // Draw centerline
        context.DrawLine(CenterPen, new Point(cx, bodyTop + 5), new Point(cx, bodyTop + bodyH - 5));

        // Draw front direction indicator (triangle)
        var triangleTop = new Point(cx, bodyTop - 5);
        var triangleLeft = new Point(cx - 8, bodyTop + 8);
        var triangleRight = new Point(cx + 8, bodyTop + 8);
        var triGeo = new StreamGeometry();
        using (var ctx = triGeo.Open())
        {
            ctx.BeginFigure(triangleTop, true);
            ctx.LineTo(triangleLeft);
            ctx.LineTo(triangleRight);
            ctx.EndFigure(true);
        }
        context.DrawGeometry(Brushes.Yellow, null, triGeo);

        // Draw rear wheels (fixed)
        DrawWheel(context, cx - axleOffset, rearAxleY, wheelW, wheelH, 0, WheelBrush, WheelPen);
        DrawWheel(context, cx + axleOffset, rearAxleY, wheelW, wheelH, 0, WheelBrush, WheelPen);

        // Draw commanded angle ghost wheels (if autosteer engaged)
        if (AutoSteerEngaged)
        {
            DrawWheel(context, cx - axleOffset, frontAxleY, wheelW, wheelH, CommandedAngle, CommandedBrush, CommandedPen);
            DrawWheel(context, cx + axleOffset, frontAxleY, wheelW, wheelH, CommandedAngle, CommandedBrush, CommandedPen);
        }

        // Draw front wheels (steered by WAS angle)
        DrawWheel(context, cx - axleOffset, frontAxleY, wheelW, wheelH, WheelAngle, WheelBrush, WheelPen);
        DrawWheel(context, cx + axleOffset, frontAxleY, wheelW, wheelH, WheelAngle, WheelBrush, WheelPen);
    }

    private static void DrawWheel(DrawingContext context, double cx, double cy,
        double width, double height, double angleDeg, IBrush fill, IPen pen)
    {
        using (context.PushTransform(
            Matrix.CreateTranslation(-cx, -cy) *
            Matrix.CreateRotation(angleDeg * Math.PI / 180.0) *
            Matrix.CreateTranslation(cx, cy)))
        {
            var rect = new Rect(cx - width / 2, cy - height / 2, width, height);
            context.DrawRectangle(fill, pen, rect, 2, 2);
        }
    }
}
