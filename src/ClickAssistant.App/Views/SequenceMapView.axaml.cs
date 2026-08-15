using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using ClickAssistant.App.Localization;
using ClickAssistant.App.ViewModels;

namespace ClickAssistant.App.Views;

public partial class SequenceMapView : UserControl
{
    private ProfileEditorViewModel? _viewModel;
    private System.Collections.ObjectModel.ObservableCollection<SequenceStepViewModel>? _observedSteps;

    /// <summary>Jeden Rectangle na monitor, udržovaný napříč přepočty mapy (klíč = MapMonitorRectViewModel.Index) -
    /// umožňuje přetahování myší: RebuildMonitorRects during pointer drag jen aktualizuje existující prvky
    /// místo Clear()+recreate, jinak by přetahování ztratilo zachycení ukazatele uprostřed gesta.</summary>
    private readonly Dictionary<int, Rectangle> _monitorShapes = new();

    private int? _draggingMonitorIndex;
    private Point _dragStartPointerPos;
    private double _dragStartX;
    private double _dragStartY;
    private bool _dragMoved;

    private readonly List<Window> _identifyOverlays = new();

    public SequenceMapView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        CloseIdentifyOverlays();

        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as ProfileEditorViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        RebuildMonitorRects();
        AttachStepsCollection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProfileEditorViewModel.MapMonitorRects) or nameof(ProfileEditorViewModel.IsIdentifyingMonitors))
            RebuildMonitorRects();

        if (e.PropertyName == nameof(ProfileEditorViewModel.IsIdentifyingMonitors))
        {
            if (_viewModel!.IsIdentifyingMonitors) ShowIdentifyOverlays();
            else CloseIdentifyOverlays();
        }
        else if (e.PropertyName == nameof(ProfileEditorViewModel.Steps))
        {
            AttachStepsCollection();
        }
    }

    /// <summary>Vykresluje obdélníky monitorů ručně místo přes ItemsControl/DataTemplate - Avalonia u tohoto
    /// ItemsControlu+Canvas+ObservableCollection spolehlivě špatně vykreslovala 2+ položky najednou (kontejnery
    /// si popletly pozici a velikost mezi sebou), zatímco ruční správa Canvas dětí funguje bez problémů.
    /// Existující Rectangle prvky se podle Indexu aktualizují na místě (nemažou/nevytvářejí znovu) - jednak je
    /// to levnější, hlavně to ale během přetahování myší zachová zachycení ukazatele (Pointer.Capture) na
    /// přetahovaném prvku, které by Clear()+recreate zrušilo uprostřed gesta.</summary>
    private void RebuildMonitorRects()
    {
        if (_viewModel is null)
        {
            MonitorRectsHost.Children.Clear();
            _monitorShapes.Clear();
            return;
        }

        var currentIndices = new HashSet<int>(_viewModel.MapMonitorRects.Select(r => r.Index));
        foreach (var staleIndex in _monitorShapes.Keys.Where(k => !currentIndices.Contains(k)).ToList())
        {
            MonitorRectsHost.Children.Remove(_monitorShapes[staleIndex]);
            _monitorShapes.Remove(staleIndex);
        }

        foreach (var staleLabel in MonitorRectsHost.Children.OfType<TextBlock>().Where(t => Equals(t.Tag, IdentifyLabelTag)).ToList())
            MonitorRectsHost.Children.Remove(staleLabel);

        foreach (var rect in _viewModel.MapMonitorRects)
        {
            if (!_monitorShapes.TryGetValue(rect.Index, out var shape))
            {
                shape = CreateMonitorShape(rect.Index);
                _monitorShapes[rect.Index] = shape;
                MonitorRectsHost.Children.Add(shape);
            }

            // Vlastní pozici právě přetahovaného monitoru neprepisovat - o ni se stará přímo pointer handler,
            // aby sledoval kurzor 1:1 (RecomputeMap doběhne se stejnou hodnotou, ale s jednotickovým zpožděním).
            if (_draggingMonitorIndex != rect.Index)
            {
                Canvas.SetLeft(shape, rect.X);
                Canvas.SetTop(shape, rect.Y);
            }
            shape.Width = rect.Width;
            shape.Height = rect.Height;
            if (rect.IsFocused) shape.Classes.Add("focused");
            else shape.Classes.Remove("focused");

            if (_viewModel.IsIdentifyingMonitors)
                MonitorRectsHost.Children.Add(BuildIdentifyLabel(rect));
        }
    }

    private const string IdentifyLabelTag = "identifyLabel";

    private static TextBlock BuildIdentifyLabel(MapMonitorRectViewModel rect)
    {
        double fontSize = Math.Clamp(Math.Min(rect.Width, rect.Height) * 0.5, 12, 40);
        var label = new TextBlock
        {
            Tag = IdentifyLabelTag,
            Text = (rect.Index + 1).ToString(),
            FontSize = fontSize,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brushes.White,
            Width = rect.Width,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, rect.X);
        Canvas.SetTop(label, rect.Y + (rect.Height - fontSize * 1.2) / 2);
        return label;
    }

    /// <summary>Vytvoří Rectangle pro jeden monitor a napojí na něj jak klik (přiblížení na monitor), tak
    /// přetažení myší (ruční přeuspořádání v mapě) - rozliší se podle toho, jestli se ukazatel mezi
    /// PointerPressed a PointerReleased posunul o víc než pár px.</summary>
    private Rectangle CreateMonitorShape(int index)
    {
        var shape = new Rectangle { Cursor = new Cursor(StandardCursorType.Hand) };
        shape.Classes.Add("monitorRect");
        ToolTip.SetTip(shape, LocalizationManager.Instance["map.monitor.focus.tooltip"]);

        shape.PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(shape).Properties.IsLeftButtonPressed) return;

            _draggingMonitorIndex = index;
            _dragStartPointerPos = args.GetPosition(MonitorRectsHost);
            _dragStartX = Canvas.GetLeft(shape);
            _dragStartY = Canvas.GetTop(shape);
            _dragMoved = false;
            args.Pointer.Capture(shape);
            args.Handled = true;
        };

        shape.PointerMoved += (_, args) =>
        {
            if (_draggingMonitorIndex != index) return;

            var current = args.GetPosition(MonitorRectsHost);
            double dx = current.X - _dragStartPointerPos.X;
            double dy = current.Y - _dragStartPointerPos.Y;
            if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3) _dragMoved = true;

            double newX = _dragStartX + dx;
            double newY = _dragStartY + dy;
            (newX, newY) = SnapToOtherMonitors(index, newX, newY, shape.Width, shape.Height);
            // UpdateMonitorManualPosition vrací výslednou pozici PO vyřešení kolizí s ostatními monitory
            // a s okrajem mapy (viz ProfileEditorViewModel.ResolveMonitorDragPosition) - ta se může lišit
            // od navržené newX/newY, takže obdélník musí sledovat právě ji, ne vlastní neomezený návrh.
            if (_viewModel is not null)
            {
                (newX, newY) = _viewModel.UpdateMonitorManualPosition(index, newX, newY);
            }
            Canvas.SetLeft(shape, newX);
            Canvas.SetTop(shape, newY);
            args.Handled = true;
        };

        shape.PointerReleased += (_, args) =>
        {
            if (_draggingMonitorIndex != index) return;

            args.Pointer.Capture(null);
            _draggingMonitorIndex = null;
            if (!_dragMoved) _viewModel?.ToggleMonitorFocusCommand.Execute(index);
            args.Handled = true;
        };

        return shape;
    }

    private const double SnapThresholdPx = 10;

    /// <summary>Stejná mezera jako mezi monitory v automatickém rozložení mapy (ProfileEditorViewModel.
    /// MonitorGapPx) - "dotykové" přichycení hranou k hraně má mít stejný odstup, ne se slepit na 0 px.</summary>
    private const double SnapGapPx = 6;

    /// <summary>Při přetahování "přichytí" hranu taženého monitoru k hraně kteréhokoliv jiného monitoru
    /// v mapě, když jsou v rámci pár pixelů (dotyk hran s mezerou SnapGapPx, nebo přesné zarovnání hran) -
    /// stejná logika na obou osách nezávisle, ať jde monitory poskládat vedle/pod sebe bez ručního doladění.</summary>
    private (double X, double Y) SnapToOtherMonitors(int draggedIndex, double x, double y, double width, double height)
    {
        if (_viewModel is null) return (x, y);

        double? snappedX = null, snappedY = null;
        double bestDx = SnapThresholdPx, bestDy = SnapThresholdPx;

        foreach (var other in _viewModel.MapMonitorRects)
        {
            if (other.Index == draggedIndex) continue;

            // Dotyk hran (s mezerou) i přesné zarovnání hran (bez mezery - řadí do stejného sloupce/řádku).
            double[] candidatesX = { other.X - width - SnapGapPx, other.X + other.Width + SnapGapPx, other.X, other.X + other.Width - width };
            foreach (var candidateX in candidatesX)
            {
                double dx = Math.Abs(candidateX - x);
                if (dx < bestDx) { bestDx = dx; snappedX = candidateX; }
            }

            double[] candidatesY = { other.Y - height - SnapGapPx, other.Y + other.Height + SnapGapPx, other.Y, other.Y + other.Height - height };
            foreach (var candidateY in candidatesY)
            {
                double dy = Math.Abs(candidateY - y);
                if (dy < bestDy) { bestDy = dy; snappedY = candidateY; }
            }
        }

        return (snappedX ?? x, snappedY ?? y);
    }

    /// <summary>Skutečný fullscreen overlay na každém fyzickém monitoru (obdoba "Identify" v nastavení
    /// displejů Windows), doplňuje malé popisky v mapě (viz RebuildMonitorRects).</summary>
    private void ShowIdentifyOverlays()
    {
        CloseIdentifyOverlays();
        if (_viewModel is null) return;

        var monitors = _viewModel.GetCurrentMonitors();
        for (int i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            var overlay = new MonitorIdentifyOverlayWindow(i + 1)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Position = new PixelPoint(monitor.X, monitor.Y),
                Width = Math.Max(1, monitor.Width / monitor.Scaling),
                Height = Math.Max(1, monitor.Height / monitor.Scaling)
            };
            overlay.Show();
            _identifyOverlays.Add(overlay);
        }
    }

    private void CloseIdentifyOverlays()
    {
        foreach (var overlay in _identifyOverlays) overlay.Close();
        _identifyOverlays.Clear();
    }

    /// <summary>Přepojí sledování na aktuální Steps kolekci ViewModelu - ta se při načtení profilu nahrazuje
    /// celá najednou (viz ProfileEditorViewModel.LoadFrom), jinak se do ní jednotlivé kroky přidávají/odebírají
    /// postupně (AddPoint/RemoveStep/MoveStepUp/Down), proto sledujeme i CollectionChanged.</summary>
    private void AttachStepsCollection()
    {
        if (_observedSteps is not null) _observedSteps.CollectionChanged -= OnStepsCollectionChanged;

        _observedSteps = _viewModel?.Steps;
        if (_observedSteps is not null) _observedSteps.CollectionChanged += OnStepsCollectionChanged;

        RebuildSteps();
    }

    private void OnStepsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildSteps();

    /// <summary>Vykresluje tečky bodů sekvence ručně místo přes ItemsControl/DataTemplate - viz komentář
    /// u RebuildMonitorRects, stejná Avalonia chyba postihuje i tento ItemsControl s 2+ položkami. Na rozdíl
    /// od monitorových obdélníků (nové instance při každém přepočtu) se stávající SequenceStepViewModel
    /// instance v Steps nemažou/nevytvářejí při každém přepočtu mapy - proto tlačítka bindujeme na live
    /// vlastnosti (MapX/MapY/StepNumber/Name), aby se sama aktualizovala i beze změny samotné kolekce Steps.</summary>
    private void RebuildSteps()
    {
        StepsHost.Children.Clear();
        if (_viewModel is null || _observedSteps is null) return;

        foreach (var step in _observedSteps)
        {
            var button = new Button
            {
                DataContext = step,
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(9),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontSize = 9,
                Command = _viewModel.SelectStepCommand,
                CommandParameter = step
            };
            button.Bind(Button.BackgroundProperty, new DynamicResourceExtension("ThemeAccentBrush"));
            button.Bind(Button.ForegroundProperty, new DynamicResourceExtension("ThemeAccentContrastBrush"));
            button.Bind(Canvas.LeftProperty, new Binding(nameof(SequenceStepViewModel.MapX)) { Source = step });
            button.Bind(Canvas.TopProperty, new Binding(nameof(SequenceStepViewModel.MapY)) { Source = step });
            button.Bind(ContentControl.ContentProperty, new Binding(nameof(SequenceStepViewModel.StepNumber)) { Source = step });
            button.Bind(ToolTip.TipProperty, new Binding(nameof(SequenceStepViewModel.Name)) { Source = step });
            StepsHost.Children.Add(button);
        }
    }
}
