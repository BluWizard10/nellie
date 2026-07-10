using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Nellie.Models;
using Nellie.ViewModels;

namespace Nellie.Views
{
    public partial class MainWindow : Window
    {
        private static readonly IBrush AccentBrush = new SolidColorBrush(Color.Parse("#7C5CFF"));
        private static readonly IBrush HeaderBrush = new SolidColorBrush(Color.Parse("#9A9AB4"));
        private const string DurationToken = "Duration";

        private readonly Dictionary<string, TextBlock> _sortArrows = new(StringComparer.OrdinalIgnoreCase);
        private MainWindowViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_viewModel is not null)
                _viewModel.ColumnsChanged -= RebuildColumns;

            _viewModel = DataContext as MainWindowViewModel;

            if (_viewModel is not null)
            {
                _viewModel.ColumnsChanged += RebuildColumns;
                RebuildColumns();
            }
        }

        private void OnPlaylistDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (_viewModel?.SelectedTrack is not null)
                _viewModel.PlayTrackCommand.Execute(_viewModel.SelectedTrack);
        }

        /// <summary>
        /// Header clicks arrive through the DataGrid's native Sorting event. We
        /// cancel its default behaviour and reorder the playlist ourselves so the
        /// natural-sort / unmatched-last rules (and playback order) stay in charge.
        /// A nested Button header would never fire — the header captures the pointer.
        /// </summary>
        private void OnPlaylistSorting(object? sender, DataGridColumnEventArgs e)
        {
            e.Handled = true;
            string? token = e.Column.SortMemberPath;
            if (_viewModel is null || string.IsNullOrEmpty(token))
                return;

            _viewModel.SortByColumn(token);
            UpdateSortArrows();
        }

        /// <summary>Regenerates the DataGrid columns from the view model's pattern tokens.</summary>
        private void RebuildColumns()
        {
            if (_viewModel is null)
                return;

            PlaylistGrid.Columns.Clear();
            _sortArrows.Clear();

            // Now-playing indicator dot (not sortable).
            PlaylistGrid.Columns.Add(new DataGridTemplateColumn
            {
                CanUserSort = false,
                CanUserResize = false,
                Width = new DataGridLength(26),
                CellTemplate = new FuncDataTemplate<Track>((_, _) =>
                {
                    var dot = new Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill = AccentBrush,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    dot.Bind(Visual.IsVisibleProperty, new Binding(nameof(Track.IsCurrent)));
                    return dot;
                }),
            });

            // One column per pattern token, bound to the track's field indexer.
            foreach (var token in _viewModel.Columns)
            {
                PlaylistGrid.Columns.Add(CreateSortableColumn(
                    token.ToUpperInvariant(),
                    token,
                    new Binding($"[{token}]"),
                    new DataGridLength(1, DataGridLengthUnitType.Star)));
            }

            // Trailing duration column.
            PlaylistGrid.Columns.Add(CreateSortableColumn(
                "TIME",
                DurationToken,
                new Binding(nameof(Track.DurationText)),
                DataGridLength.Auto));

            UpdateSortArrows();
        }

        private DataGridTextColumn CreateSortableColumn(string title, string token, Binding cellBinding, DataGridLength width)
        {
            var label = new TextBlock
            {
                Text = title,
                Foreground = HeaderBrush,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var arrow = new TextBlock
            {
                Foreground = AccentBrush,
                FontSize = 11,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _sortArrows[token] = arrow;

            // A plain StackPanel header does not capture the pointer, so the
            // DataGrid's header-click / Sorting pipeline works normally.
            return new DataGridTextColumn
            {
                Header = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { label, arrow },
                },
                Binding = cellBinding,
                Width = width,
                IsReadOnly = true,
                CanUserSort = true,
                SortMemberPath = token,
            };
        }

        private void UpdateSortArrows()
        {
            if (_viewModel is null)
                return;

            foreach (var (token, arrow) in _sortArrows)
            {
                bool active = string.Equals(token, _viewModel.SelectedSortField, StringComparison.OrdinalIgnoreCase);
                arrow.Text = active ? (_viewModel.SortDescending ? "▼" : "▲") : string.Empty;
            }
        }
    }
}
