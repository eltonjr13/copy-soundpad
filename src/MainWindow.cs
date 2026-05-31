using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using Microsoft.Win32;
using SoundDeck.Models;
using SoundDeck.Services;

namespace SoundDeck
{
    public class MainWindow : Window
    {
        // Cores do Tema Escuro Premium (Ciano/Teal)
        private static readonly Brush ColorBgMain = new SolidColorBrush(Color.FromRgb(15, 15, 17));       // #0F0F11
        private static readonly Brush ColorBgSidebar = new SolidColorBrush(Color.FromRgb(23, 23, 26));    // #17171A
        private static readonly Brush ColorBgCard = new SolidColorBrush(Color.FromRgb(28, 28, 33));       // #1C1C21
        private static readonly Brush ColorBgCardHover = new SolidColorBrush(Color.FromRgb(37, 37, 43));  // #25252B
        private static readonly Brush ColorAccent = new SolidColorBrush(Color.FromRgb(0, 173, 181));      // #00ADB5
        private static readonly Brush ColorAccentHover = new SolidColorBrush(Color.FromRgb(0, 210, 219)); // #00D2DB
        private static readonly Brush ColorTextPrimary = new SolidColorBrush(Color.FromRgb(238, 238, 238)); // #EEEEEE
        private static readonly Brush ColorTextSecondary = new SolidColorBrush(Color.FromRgb(144, 144, 150)); // #909096
        private static readonly Brush ColorBorder = new SolidColorBrush(Color.FromRgb(43, 43, 48));       // #2B2B30
        private static readonly Brush ColorDanger = new SolidColorBrush(Color.FromRgb(226, 62, 87));      // #E23E57

        private SoundDeckConfig _config;
        private string _activeCategory = "Todos";
        private string _searchQuery = string.Empty;
        
        // Elementos da Interface
        private StackPanel _sidebarList;
        private StackPanel _soundListContainer;
        private ComboBox _deviceComboBox;
        private ComboBox _monitorComboBox;
        private TextBox _searchTextBox;
        private Slider _masterVolumeSlider;
        private TextBlock _emptyStateTextBlock;
        
        // Estado de Captura de Hotkey
        private SoundItem _hotkeyCaptureSound;
        private Border _hotkeyCaptureButton;

        // Controle da bandeja do sistema (System Tray)
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private bool _isExiting = false;

        public MainWindow()
        {
            // Carregar configurações locais
            _config = SettingsService.LoadConfig();

            // Configurações do Áudio Player Service
            AudioPlayerService.SelectedDeviceName = _config.SelectedDevice;
            AudioPlayerService.SelectedMonitorName = _config.SelectedMonitor;
            AudioPlayerService.MasterVolume = _config.MasterVolume;

            // Configurar Janela
            Title = "SoundDeck";
            Width = 950;
            Height = 650;
            MinWidth = 800;
            MinHeight = 500;
            Background = ColorBgMain;
            Foreground = ColorTextPrimary;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI, Malgun Gothic, Helvetica");

            // Inicializar a Bandeja do Sistema
            InitializeTrayIcon();

            // Montar Layout Principal
            InitializeLayout();

            // Habilitar Drag & Drop
            AllowDrop = true;
            Drop += MainWindow_Drop;
            DragOver += MainWindow_DragOver;

            // Escutar eventos de tecla na janela para captura de Hotkey
            PreviewKeyDown += MainWindow_PreviewKeyDown;

            // Tratar fechamento de janela
            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;
        }

        #region Inicialização da Interface

        private void InitializeLayout()
        {
            // Grid Principal: 2 Colunas (Sidebar e Conteúdo Principal)
            Grid mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) }); // Sidebar
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Principal

            // --- 1. BARRA LATERAL (SIDEBAR) ---
            Border sidebarBorder = new Border
            {
                Background = ColorBgSidebar,
                BorderBrush = ColorBorder,
                BorderThickness = new Thickness(0, 0, 1, 0)
            };

            Grid sidebarGrid = new Grid();
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Logo
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Lista de Categorias
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Botão Adicionar Categoria

            // Logo do App
            Border logoBorder = new Border
            {
                Padding = new Thickness(20, 25, 20, 25)
            };
            TextBlock logoText = new TextBlock
            {
                Text = "🔊 SoundDeck",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = ColorAccent
            };
            logoBorder.Child = logoText;
            Grid.SetRow(logoBorder, 0);
            sidebarGrid.Children.Add(logoBorder);

            // Lista de Categorias (ScrollViewer para rolagem)
            ScrollViewer categoryScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10, 0, 10, 0)
            };
            _sidebarList = new StackPanel();
            categoryScrollViewer.Content = _sidebarList;
            Grid.SetRow(categoryScrollViewer, 1);
            sidebarGrid.Children.Add(categoryScrollViewer);

            // Botão Adicionar Categoria
            Border addCategoryBtn = CreateInteractiveButton("➕ Nova Categoria", ColorAccent, ColorTextPrimary, () =>
            {
                PromptForNewCategory();
            });
            addCategoryBtn.Margin = new Thickness(15, 15, 15, 20);
            Grid.SetRow(addCategoryBtn, 2);
            sidebarGrid.Children.Add(addCategoryBtn);

            sidebarBorder.Child = sidebarGrid;
            Grid.SetColumn(sidebarBorder, 0);
            mainGrid.Children.Add(sidebarBorder);

            // --- 2. PAINEL PRINCIPAL (CONTEÚDO) ---
            Grid contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Top Bar
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Sound Grid

            // Top Bar
            Border topBarBorder = new Border
            {
                Padding = new Thickness(20, 20, 20, 15),
                BorderBrush = ColorBorder,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            Grid topBarGrid = new Grid();
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Campo Busca
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // Seletor Dispositivo
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // Seletor Monitoramento
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); // Volume Geral
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) }); // Importar Áudio

            // Campo Busca
            Border searchBorder = new Border
            {
                Background = ColorBgSidebar,
                CornerRadius = new CornerRadius(6),
                BorderBrush = ColorBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 10, 0),
                Height = 35
            };
            Grid searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock searchIcon = new TextBlock
            {
                Text = "🔍",
                Foreground = ColorTextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(searchIcon, 0);
            searchGrid.Children.Add(searchIcon);

            _searchTextBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = ColorTextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                CaretBrush = ColorAccent
            };
            // Placeholder/Dica de busca
            _searchTextBox.TextChanged += (s, e) =>
            {
                _searchQuery = _searchTextBox.Text;
                RefreshSoundList();
            };
            Grid.SetColumn(_searchTextBox, 1);
            searchGrid.Children.Add(_searchTextBox);
            searchBorder.Child = searchGrid;
            Grid.SetColumn(searchBorder, 0);
            topBarGrid.Children.Add(searchBorder);

            // Seletor Dispositivo de Áudio
            _deviceComboBox = new ComboBox
            {
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Background = ColorBgSidebar,
                Foreground = ColorTextPrimary,
                BorderBrush = ColorBorder,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 8, 0),
                ToolTip = "Dispositivo de Transmissão (ex: Canal Virtual de Áudio)"
            };
            RefreshAudioDevices();
            _deviceComboBox.SelectionChanged += (s, e) =>
            {
                if (_deviceComboBox.SelectedItem != null)
                {
                    string selectedDevice = _deviceComboBox.SelectedItem.ToString();
                    _config.SelectedDevice = selectedDevice;
                    AudioPlayerService.SelectedDeviceName = selectedDevice;
                    SettingsService.SaveConfig(_config);
                }
            };
            Grid.SetColumn(_deviceComboBox, 1);
            topBarGrid.Children.Add(_deviceComboBox);

            // Seletor Dispositivo de Monitoramento
            _monitorComboBox = new ComboBox
            {
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Background = ColorBgSidebar,
                Foreground = ColorTextPrimary,
                BorderBrush = ColorBorder,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 8, 0),
                ToolTip = "Dispositivo de Monitoramento (Seus fones/alto-falantes para você ouvir)"
            };
            RefreshMonitorDevices();
            _monitorComboBox.SelectionChanged += (s, e) =>
            {
                if (_monitorComboBox.SelectedItem != null)
                {
                    string selectedMonitor = _monitorComboBox.SelectedItem.ToString();
                    _config.SelectedMonitor = selectedMonitor;
                    AudioPlayerService.SelectedMonitorName = selectedMonitor;
                    SettingsService.SaveConfig(_config);
                }
            };
            Grid.SetColumn(_monitorComboBox, 2);
            topBarGrid.Children.Add(_monitorComboBox);

            // Volume Geral Slider
            StackPanel volStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0)
            };
            TextBlock volIcon = new TextBlock
            {
                Text = "🔊",
                Foreground = ColorTextSecondary,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            volStack.Children.Add(volIcon);

            _masterVolumeSlider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = _config.MasterVolume * 100,
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ColorAccent
            };
            _masterVolumeSlider.ValueChanged += (s, e) =>
            {
                float newVol = (float)(_masterVolumeSlider.Value / 100.0);
                _config.MasterVolume = newVol;
                AudioPlayerService.MasterVolume = newVol;
                
                // Salvar após pequeno delay ou na liberação do slider. Para simplificar, salva instantâneo.
                SettingsService.SaveConfig(_config);
            };
            volStack.Children.Add(_masterVolumeSlider);
            Grid.SetColumn(volStack, 3);
            topBarGrid.Children.Add(volStack);

            // Botão Importar
            Border importBtn = CreateInteractiveButton("📥 Importar Som", ColorAccent, ColorTextPrimary, () =>
            {
                ImportSoundWithDialog();
            });
            importBtn.Height = 35;
            Grid.SetColumn(importBtn, 4);
            topBarGrid.Children.Add(importBtn);

            topBarBorder.Child = topBarGrid;
            Grid.SetRow(topBarBorder, 0);
            contentGrid.Children.Add(topBarBorder);

            // Lista Principal de Áudios (ScrollViewer contendo StackPanel de cards)
            ScrollViewer soundScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20, 15, 20, 20)
            };

            Grid soundContainerGrid = new Grid();
            _soundListContainer = new StackPanel();
            soundContainerGrid.Children.Add(_soundListContainer);

            // Texto Estado Vazio
            _emptyStateTextBlock = new TextBlock
            {
                Text = "Arraste e solte arquivos de áudio aqui\nou clique em 'Importar Som' para começar.",
                Foreground = ColorTextSecondary,
                FontSize = 16,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 100, 0, 0),
                Visibility = Visibility.Collapsed
            };
            soundContainerGrid.Children.Add(_emptyStateTextBlock);

            soundScrollViewer.Content = soundContainerGrid;
            Grid.SetRow(soundScrollViewer, 1);
            contentGrid.Children.Add(soundScrollViewer);

            Grid.SetColumn(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            Content = mainGrid;

            // Renderizar itens iniciais
            RefreshCategoryList();
            RefreshSoundList();
        }

        // Helper para criar botões interativos estilizados e responsivos (Hover micro-animations)
        private Border CreateInteractiveButton(string text, Brush activeColor, Brush textColor, Action onClick, double fontSize = 13)
        {
            Border border = new Border
            {
                Background = activeColor,
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = textColor,
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = textBlock;

            border.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    onClick();
                    e.Handled = true;
                }
            };

            border.MouseEnter += (s, e) =>
            {
                if (activeColor == ColorAccent)
                    border.Background = ColorAccentHover;
                else
                    border.Background = ColorBgCardHover;
            };

            border.MouseLeave += (s, e) =>
            {
                border.Background = activeColor;
            };

            return border;
        }

        #endregion

        #region Renderização Dinâmica (Sidebar e Sons)

        // Atualiza a lista de categorias na barra lateral
        private void RefreshCategoryList()
        {
            _sidebarList.Children.Clear();

            foreach (var category in _config.Categories)
            {
                bool isSelected = category.Name.Equals(_activeCategory, StringComparison.OrdinalIgnoreCase);

                Border categoryItem = new Border
                {
                    Background = isSelected ? ColorAccent : Brushes.Transparent,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(15, 10, 15, 10),
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = Cursors.Hand
                };

                Grid catGrid = new Grid();
                catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Ícone
                catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Nome
                catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Ação de Excluir se aplicável

                TextBlock iconTb = new TextBlock
                {
                    Text = category.Icon,
                    Margin = new Thickness(0, 0, 10, 0),
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(iconTb, 0);
                catGrid.Children.Add(iconTb);

                TextBlock nameTb = new TextBlock
                {
                    Text = category.Name,
                    Foreground = isSelected ? Brushes.White : ColorTextPrimary,
                    FontSize = 14,
                    FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(nameTb, 1);
                catGrid.Children.Add(nameTb);

                // Exibir botão de exclusão apenas para categorias customizadas (não as padrão)
                string catName = category.Name;
                bool isDefaultCat = new[] { "Todos", "Favoritos", "Memes", "Efeitos", "Músicas", "Vozes" }.Contains(catName);
                if (!isDefaultCat)
                {
                    TextBlock deleteCatBtn = new TextBlock
                    {
                        Text = "❌",
                        FontSize = 10,
                        Foreground = isSelected ? Brushes.White : ColorDanger,
                        Margin = new Thickness(5, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = Cursors.Hand
                    };
                    deleteCatBtn.MouseDown += (s, e) =>
                    {
                        if (e.ChangedButton == MouseButton.Left)
                        {
                            DeleteCategory(catName);
                            e.Handled = true; // Impede a seleção da categoria ao mesmo tempo
                        }
                    };
                    Grid.SetColumn(deleteCatBtn, 2);
                    catGrid.Children.Add(deleteCatBtn);
                }

                categoryItem.Child = catGrid;

                // Eventos de clique e hover
                categoryItem.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        _activeCategory = catName;
                        RefreshCategoryList();
                        RefreshSoundList();
                    }
                };

                if (!isSelected)
                {
                    categoryItem.MouseEnter += (s, e) => categoryItem.Background = ColorBgCard;
                    categoryItem.MouseLeave += (s, e) => categoryItem.Background = Brushes.Transparent;
                }

                _sidebarList.Children.Add(categoryItem);
            }
        }

        // Renderiza e atualiza os cards de áudio com base no filtro atual
        private void RefreshSoundList()
        {
            _soundListContainer.Children.Clear();

            // Filtrar sons
            IEnumerable<SoundItem> filteredSons = _config.Sounds;

            // Filtro por Categoria
            if (_activeCategory.Equals("Favoritos", StringComparison.OrdinalIgnoreCase))
            {
                filteredSons = filteredSons.Where(s => s.IsFavorite);
            }
            else if (!_activeCategory.Equals("Todos", StringComparison.OrdinalIgnoreCase))
            {
                filteredSons = filteredSons.Where(s => s.CategoryName.Equals(_activeCategory, StringComparison.OrdinalIgnoreCase));
            }

            // Filtro por Busca
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                filteredSons = filteredSons.Where(s => s.Name.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var listSons = filteredSons.ToList();

            if (listSons.Count == 0)
            {
                _emptyStateTextBlock.Visibility = Visibility.Visible;
                return;
            }

            _emptyStateTextBlock.Visibility = Visibility.Collapsed;

            // Criar elementos UI para cada som
            foreach (var sound in listSons)
            {
                Border card = new Border
                {
                    Background = ColorBgCard,
                    BorderBrush = ColorBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(15, 10, 15, 10),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                Grid rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Play/Stop
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Nome, Duração, Tipo
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Categoria
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) }); // Hotkey
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // Volume Individual
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Ações (Fav, Excluir)

                // 1. Botão Play / Stop
                bool isPlaying = AudioPlayerService.IsPlaying(sound.Id);
                Border playStopBtn = new Border
                {
                    Background = ColorBgMain,
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(16),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 0, 15, 0),
                    BorderBrush = ColorBorder,
                    BorderThickness = new Thickness(1)
                };
                TextBlock playStopIcon = new TextBlock
                {
                    Text = isPlaying ? "⏹" : "▶",
                    Foreground = ColorAccent,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(isPlaying ? 0 : 2, 0, 0, 0)
                };
                playStopBtn.Child = playStopIcon;
                playStopBtn.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        TogglePlay(sound, playStopIcon);
                    }
                };
                playStopBtn.MouseEnter += (s, e) => playStopBtn.Background = ColorBgCardHover;
                playStopBtn.MouseLeave += (s, e) => playStopBtn.Background = ColorBgMain;

                Grid.SetColumn(playStopBtn, 0);
                rowGrid.Children.Add(playStopBtn);

                // 2. Info do Áudio (Nome, Duração, Formato)
                StackPanel infoPanel = new StackPanel();
                TextBlock nameTb = new TextBlock
                {
                    Text = sound.Name,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = ColorTextPrimary,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                infoPanel.Children.Add(nameTb);

                StackPanel detailsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
                TextBlock durationTb = new TextBlock
                {
                    Text = string.Format("⏱ {0}", sound.DurationText),
                    Foreground = ColorTextSecondary,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                detailsPanel.Children.Add(durationTb);

                // Extensão/Tipo do Arquivo em Badge
                string ext = Path.GetExtension(sound.FilePath).Replace(".", "").ToUpper();
                Border badgeBorder = new Border
                {
                    Background = ColorBgMain,
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(5, 1, 5, 1),
                    BorderBrush = ColorBorder,
                    BorderThickness = new Thickness(1)
                };
                TextBlock badgeText = new TextBlock
                {
                    Text = ext,
                    Foreground = ColorTextSecondary,
                    FontSize = 9,
                    FontWeight = FontWeights.Bold
                };
                badgeBorder.Child = badgeText;
                detailsPanel.Children.Add(badgeBorder);

                infoPanel.Children.Add(detailsPanel);
                Grid.SetColumn(infoPanel, 1);
                rowGrid.Children.Add(infoPanel);

                // 3. Badge da Categoria
                Border catBadge = new Border
                {
                    Background = ColorBgMain,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 0, 15, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    BorderBrush = ColorBorder,
                    BorderThickness = new Thickness(1)
                };
                TextBlock catText = new TextBlock
                {
                    Text = sound.CategoryName,
                    Foreground = ColorTextSecondary,
                    FontSize = 11
                };
                catBadge.Child = catText;
                Grid.SetColumn(catBadge, 2);
                rowGrid.Children.Add(catBadge);

                // 4. Seletor de Hotkey
                bool hasHotkey = !string.IsNullOrEmpty(sound.Hotkey);
                Border hotkeyBorder = new Border
                {
                    Background = ColorBgMain,
                    CornerRadius = new CornerRadius(5),
                    BorderBrush = ColorBorder,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 5, 8, 5),
                    Cursor = Cursors.Hand,
                    Height = 28,
                    Width = 125,
                    Margin = new Thickness(0, 0, 15, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                TextBlock hotkeyText = new TextBlock
                {
                    Text = hasHotkey ? sound.Hotkey : "Definir Atalho",
                    Foreground = hasHotkey ? ColorAccent : ColorTextSecondary,
                    FontWeight = hasHotkey ? FontWeights.Bold : FontWeights.Normal,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                hotkeyBorder.Child = hotkeyText;

                hotkeyBorder.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        StartHotkeyCapture(sound, hotkeyBorder, hotkeyText);
                        e.Handled = true;
                    }
                };

                Grid.SetColumn(hotkeyBorder, 3);
                rowGrid.Children.Add(hotkeyBorder);

                // 5. Volume Individual (Slider)
                StackPanel volStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 15, 0)
                };
                TextBlock volLabel = new TextBlock
                {
                    Text = "🔈",
                    FontSize = 11,
                    Foreground = ColorTextSecondary,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 5, 0)
                };
                volStack.Children.Add(volLabel);

                Slider itemVolSlider = new Slider
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = sound.Volume * 100,
                    Width = 70,
                    VerticalAlignment = VerticalAlignment.Center
                };
                itemVolSlider.ValueChanged += (s, e) =>
                {
                    float newVol = (float)(itemVolSlider.Value / 100.0);
                    sound.Volume = newVol;
                    AudioPlayerService.UpdateActiveVolume(sound.Id, newVol, _config.MasterVolume);
                    
                    // Salvar alteração de volume
                    SettingsService.SaveConfig(_config);
                };
                volStack.Children.Add(itemVolSlider);

                Grid.SetColumn(volStack, 4);
                rowGrid.Children.Add(volStack);

                // 6. Painel de Ações (Favoritar, Excluir)
                StackPanel actionPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Botão Estrela Favorito
                TextBlock favBtn = new TextBlock
                {
                    Text = sound.IsFavorite ? "★" : "☆",
                    Foreground = sound.IsFavorite ? Brushes.Gold : ColorTextSecondary,
                    FontSize = 18,
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                favBtn.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        sound.IsFavorite = !sound.IsFavorite;
                        SettingsService.SaveConfig(_config);
                        RefreshSoundList();
                        RefreshCategoryList();
                    }
                };
                actionPanel.Children.Add(favBtn);

                // Botão Excluir
                TextBlock deleteBtn = new TextBlock
                {
                    Text = "🗑️",
                    Foreground = ColorTextSecondary,
                    FontSize = 14,
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };
                deleteBtn.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        DeleteSound(sound);
                    }
                };
                deleteBtn.MouseEnter += (s, e) => deleteBtn.Foreground = ColorDanger;
                deleteBtn.MouseLeave += (s, e) => deleteBtn.Foreground = ColorTextSecondary;
                actionPanel.Children.Add(deleteBtn);

                Grid.SetColumn(actionPanel, 5);
                rowGrid.Children.Add(actionPanel);

                card.Child = rowGrid;
                
                // Adiciona efeito de hover no card
                card.MouseEnter += (s, e) => card.Background = ColorBgCardHover;
                card.MouseLeave += (s, e) => card.Background = ColorBgCard;

                _soundListContainer.Children.Add(card);
            }
        }

        // Recarrega os dispositivos no dropdown
        private void RefreshAudioDevices()
        {
            _deviceComboBox.Items.Clear();
            var devices = AudioPlayerService.GetOutputDevices();
            foreach (var d in devices)
            {
                _deviceComboBox.Items.Add(d);
            }

            // Selecionar o correto salvo
            if (devices.Contains(_config.SelectedDevice))
            {
                _deviceComboBox.SelectedItem = _config.SelectedDevice;
            }
            else
            {
                _deviceComboBox.SelectedIndex = 0; // Padrão
            }
        }

        // Recarrega os dispositivos no dropdown de monitoramento
        private void RefreshMonitorDevices()
        {
            _monitorComboBox.Items.Clear();
            _monitorComboBox.Items.Add("Nenhum");

            var devices = AudioPlayerService.GetOutputDevices();
            foreach (var d in devices)
            {
                if (d != "Padrão")
                {
                    _monitorComboBox.Items.Add(d);
                }
            }

            if (_monitorComboBox.Items.Contains(_config.SelectedMonitor))
            {
                _monitorComboBox.SelectedItem = _config.SelectedMonitor;
            }
            else
            {
                _monitorComboBox.SelectedIndex = 0; // Nenhum
            }
        }

        #endregion

        #region Ações e Comportamentos do Soundboard

        // Alterna entre tocar e parar
        private void TogglePlay(SoundItem sound, TextBlock playStopIcon)
        {
            try
            {
                if (AudioPlayerService.IsPlaying(sound.Id))
                {
                    AudioPlayerService.Stop(sound.Id);
                    playStopIcon.Text = "▶";
                }
                else
                {
                    AudioPlayerService.Play(sound, _config.MasterVolume, () =>
                    {
                        // Quando terminar a reprodução, rodar na thread UI para atualizar o botão
                        Dispatcher.Invoke(() =>
                        {
                            RefreshSoundList();
                        });
                    });
                    playStopIcon.Text = "⏹";
                    
                    // Pequeno delay para redesenhar a lista de forma que reflita o estado de reprodução
                    RefreshSoundList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Erro de reprodução: {0}", ex.Message), "SoundDeck", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Importa um som via diálogo de arquivo
        private void ImportSoundWithDialog()
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Arquivos de Áudio (*.mp3;*.wav;*.flac;*.ogg)|*.mp3;*.wav;*.flac;*.ogg|Todos os arquivos (*.*)|*.*",
                Title = "Selecionar arquivo de som",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (string filePath in dlg.FileNames)
                {
                    ImportSoundFile(filePath);
                }
                RefreshSoundList();
            }
        }

        // Importa um arquivo de som no modelo do SoundDeck
        private void ImportSoundFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            string name = Path.GetFileNameWithoutExtension(filePath);
            
            // Definir a categoria apropriada para o som adicionado
            string category = _activeCategory;
            // Se estiver em "Todos" ou "Favoritos", adiciona a "Memes" ou à primeira categoria customizada
            if (category == "Todos" || category == "Favoritos")
            {
                var defaultCat = _config.Categories.FirstOrDefault(c => c.Name != "Todos" && c.Name != "Favoritos");
                category = defaultCat != null ? defaultCat.Name : "Memes";
            }

            // Validar se o som já existe
            if (_config.Sounds.Any(s => s.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
            {
                return; // Já importado
            }

            // Obter a duração do arquivo de forma assíncrona/segura
            string duration = AudioPlayerService.GetDurationText(filePath);

            var newSound = new SoundItem
            {
                Name = name,
                FilePath = filePath,
                DurationText = duration,
                CategoryName = category
            };

            _config.Sounds.Add(newSound);
            SettingsService.SaveConfig(_config);
        }

        // Exclui um som
        private void DeleteSound(SoundItem sound)
        {
            var result = MessageBox.Show(string.Format("Deseja realmente remover o som '{0}' da sua lista?", sound.Name), "Remover Som", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                // Parar se estiver tocando
                AudioPlayerService.Stop(sound.Id);

                // Unregister hotkey se houver
                if (sound.HotkeyId > 0)
                {
                    IntPtr handle = new WindowInteropHelper(this).Handle;
                    HotkeyService.Unregister(handle, sound.HotkeyId);
                }

                _config.Sounds.Remove(sound);
                SettingsService.SaveConfig(_config);
                RefreshSoundList();
            }
        }

        // Prompt simples para criar categoria
        private void PromptForNewCategory()
        {
            // Criar uma mini-janela modal de input para evitar dependências de outras bibliotecas
            Window inputWindow = new Window
            {
                Title = "Nova Categoria",
                Width = 350,
                Height = 180,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = ColorBgMain,
                BorderBrush = ColorBorder,
                BorderThickness = new Thickness(1)
            };

            StackPanel panel = new StackPanel { Margin = new Thickness(15) };
            
            TextBlock label = new TextBlock { Text = "Nome da Categoria:", Foreground = ColorTextPrimary, Margin = new Thickness(0, 0, 0, 8), FontSize = 13 };
            panel.Children.Add(label);

            TextBox inputTxt = new TextBox { Height = 28, Background = ColorBgSidebar, Foreground = ColorTextPrimary, BorderBrush = ColorBorder, Margin = new Thickness(0, 0, 0, 15), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(5, 0, 5, 0) };
            panel.Children.Add(inputTxt);

            Grid btnGrid = new Grid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition());
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition());

            Border cancelBtn = CreateInteractiveButton("Cancelar", ColorBgSidebar, ColorTextPrimary, () => inputWindow.Close());
            cancelBtn.Margin = new Thickness(0, 0, 5, 0);
            Grid.SetColumn(cancelBtn, 0);
            btnGrid.Children.Add(cancelBtn);

            Border okBtn = CreateInteractiveButton("Criar", ColorAccent, ColorTextPrimary, () =>
            {
                string text = inputTxt.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    // Evitar duplicados
                    if (!_config.Categories.Any(c => c.Name.Equals(text, StringComparison.OrdinalIgnoreCase)))
                    {
                        _config.Categories.Add(new Category(text));
                        SettingsService.SaveConfig(_config);
                        RefreshCategoryList();
                        inputWindow.Close();
                    }
                    else
                    {
                        MessageBox.Show("Já existe uma categoria com este nome.", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            });
            okBtn.Margin = new Thickness(5, 0, 0, 0);
            Grid.SetColumn(okBtn, 1);
            btnGrid.Children.Add(okBtn);

            panel.Children.Add(btnGrid);
            inputWindow.Content = panel;
            
            inputWindow.ShowDialog();
        }

        // Exclui uma categoria customizada
        private void DeleteCategory(string name)
        {
            var result = MessageBox.Show(string.Format("Deseja realmente remover a categoria '{0}'?\nOs sons pertencentes a ela não serão excluídos, mas serão movidos para a categoria 'Memes'.", name), "Remover Categoria", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                // Remover categoria
                var category = _config.Categories.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (category != null)
                {
                    _config.Categories.Remove(category);
                }

                // Mover sons para Memes
                foreach (var sound in _config.Sounds)
                {
                    if (sound.CategoryName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        sound.CategoryName = "Memes";
                    }
                }

                if (_activeCategory.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    _activeCategory = "Todos";
                }

                SettingsService.SaveConfig(_config);
                RefreshCategoryList();
                RefreshSoundList();
            }
        }

        #endregion

        #region Drag and Drop e DragOver

        private void MainWindow_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".mp3" || ext == ".wav" || ext == ".flac" || ext == ".ogg")
                    {
                        ImportSoundFile(file);
                    }
                }
                RefreshSoundList();
            }
        }

        #endregion

        #region Captura de Atalhos Globais (Teclado)

        // Inicializa o modo de captura de atalho para um som
        private void StartHotkeyCapture(SoundItem sound, Border border, TextBlock textBlock)
        {
            // Se já estiver capturando, cancela o anterior
            if (_hotkeyCaptureSound != null && _hotkeyCaptureButton != null)
            {
                ResetHotkeyButtonVisual(_hotkeyCaptureSound, _hotkeyCaptureButton);
            }

            _hotkeyCaptureSound = sound;
            _hotkeyCaptureButton = border;

            border.Background = ColorDanger; // Destaca o botão em vermelho durante a captura
            textBlock.Text = "Pressione as teclas...";
            textBlock.Foreground = Brushes.White;
            border.Focus();
        }

        // Restaura a visualização do botão de hotkey para seu estado normal
        private void ResetHotkeyButtonVisual(SoundItem sound, Border border)
        {
            if (border == null) return;
            
            var textBlock = border.Child as TextBlock;
            if (textBlock == null) return;

            bool hasHotkey = !string.IsNullOrEmpty(sound.Hotkey);
            border.Background = ColorBgMain;
            textBlock.Text = hasHotkey ? sound.Hotkey : "Definir Atalho";
            textBlock.Foreground = hasHotkey ? ColorAccent : ColorTextSecondary;
            textBlock.FontWeight = hasHotkey ? FontWeights.Bold : FontWeights.Normal;
        }

        // Intercepta e processa as teclas no formulário para salvar o atalho
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_hotkeyCaptureSound == null || _hotkeyCaptureButton == null) return;

            e.Handled = true; // Impede que o comando seja espalhado pelo sistema

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Se pressionar ESC ou Backspace, limpa a hotkey
            if (key == Key.Escape || key == Key.Back)
            {
                UpdateSoundHotkey(_hotkeyCaptureSound, string.Empty);
                ResetHotkeyButtonVisual(_hotkeyCaptureSound, _hotkeyCaptureButton);
                _hotkeyCaptureSound = null;
                _hotkeyCaptureButton = null;
                RefreshSoundList();
                return;
            }

            // Ignorar teclas modificadoras puras
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            // Construir String de Atalho
            List<string> modifiers = new List<string>();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers.Add("Ctrl");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers.Add("Alt");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers.Add("Shift");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers.Add("Win");

            modifiers.Add(key.ToString());
            string newHotkey = string.Join("+", modifiers);

            // Atualizar e registrar
            UpdateSoundHotkey(_hotkeyCaptureSound, newHotkey);
            _hotkeyCaptureSound = null;
            _hotkeyCaptureButton = null;
            RefreshSoundList();
        }

        // Registra de fato a hotkey no Windows e atualiza configurações
        private void UpdateSoundHotkey(SoundItem sound, string newHotkeyText)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;

            // 1. Remover registro anterior se houver
            if (sound.HotkeyId > 0)
            {
                HotkeyService.Unregister(handle, sound.HotkeyId);
            }
            else
            {
                // Atribui uma ID única se for a primeira vez
                int maxId = 999;
                foreach (var s in _config.Sounds)
                {
                    if (s.HotkeyId > maxId) maxId = s.HotkeyId;
                }
                sound.HotkeyId = maxId + 1;
            }

            sound.Hotkey = newHotkeyText;

            // 2. Tentar registrar a nova hotkey
            if (!string.IsNullOrEmpty(newHotkeyText))
            {
                bool success = HotkeyService.Register(handle, sound.HotkeyId, newHotkeyText);
                if (!success)
                {
                    MessageBox.Show(string.Format("O atalho '{0}' não pôde ser registrado. Talvez já esteja em uso por outro programa do Windows.", newHotkeyText), "Conflito de Atalho", MessageBoxButton.OK, MessageBoxImage.Warning);
                    sound.Hotkey = string.Empty;
                }
            }

            SettingsService.SaveConfig(_config);
        }

        #endregion

        #region Registro WndProc do WPF para tratar atalhos globais

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Obter handle da janela e adicionar o hook WndProc
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);
            source.AddHook(HwndHook);

            // Registrar todas as Hotkeys salvas na inicialização
            int nextId = 1000;
            foreach (var sound in _config.Sounds)
            {
                if (!string.IsNullOrEmpty(sound.Hotkey))
                {
                    sound.HotkeyId = nextId++;
                    bool success = HotkeyService.Register(handle, sound.HotkeyId, sound.Hotkey);
                    if (!success)
                    {
                        sound.Hotkey = string.Empty; // Reseta se falhar no registro global
                    }
                }
            }
            SettingsService.SaveConfig(_config);
            RefreshSoundList();
        }

        // Método chamado sempre que o Windows envia uma mensagem para a fila da janela
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                // Encontrar o som que corresponde ao HotkeyId disparado
                var sound = _config.Sounds.FirstOrDefault(s => s.HotkeyId == hotkeyId);
                if (sound != null)
                {
                    // Toca o som imediatamente!
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            AudioPlayerService.Play(sound, _config.MasterVolume, () =>
                            {
                                Dispatcher.Invoke(() => RefreshSoundList());
                            });
                            RefreshSoundList(); // Atualiza UI para mostrar o botão Stop (⏹)
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("Erro ao reproduzir hotkey global: {0}", ex.Message));
                        }
                    });
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        #endregion

        #region Bandeja do Sistema e Gerenciamento do Ciclo de Vida

        private void InitializeTrayIcon()
        {
            // Criar ícone da bandeja do sistema usando Windows Forms (NotifyIcon)
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "SoundDeck - Soundboard Profissional",
                Visible = true
            };

            // Usar o ícone padrão de som do Windows
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;

            // Clique duplo na bandeja re-exibe o aplicativo
            _notifyIcon.DoubleClick += (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            };

            // Menu de contexto da bandeja
            var contextMenu = new System.Windows.Forms.ContextMenu();
            
            var openItem = new System.Windows.Forms.MenuItem("Abrir SoundDeck");
            openItem.Click += (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            };
            contextMenu.MenuItems.Add(openItem);

            var stopAllItem = new System.Windows.Forms.MenuItem("Parar Todos os Sons");
            stopAllItem.Click += (s, e) =>
            {
                AudioPlayerService.StopAll();
                RefreshSoundList();
            };
            contextMenu.MenuItems.Add(stopAllItem);

            contextMenu.MenuItems.Add("-"); // Separador

            var exitItem = new System.Windows.Forms.MenuItem("Sair");
            exitItem.Click += (s, e) =>
            {
                _isExiting = true;
                Close();
            };
            contextMenu.MenuItems.Add(exitItem);

            _notifyIcon.ContextMenu = contextMenu;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Se o usuário clicou para fechar e configurou minimizar para a bandeja
            if (!_isExiting && _config.MinimizeToTray)
            {
                e.Cancel = true;
                Hide(); // Apenas esconde a janela e mantém rodando na bandeja
                _notifyIcon.ShowBalloonTip(2000, "SoundDeck", "O aplicativo foi minimizado para a bandeja do sistema e continua escutando seus atalhos.", System.Windows.Forms.ToolTipIcon.Info);
            }
            else
            {
                // Desregistrar todas as hotkeys antes de fechar
                IntPtr handle = new WindowInteropHelper(this).Handle;
                foreach (var sound in _config.Sounds)
                {
                    if (sound.HotkeyId > 0)
                    {
                        HotkeyService.Unregister(handle, sound.HotkeyId);
                    }
                }
                
                // Descartar recursos de áudio
                AudioPlayerService.StopAll();

                // Destruir ícone da bandeja
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _config.MinimizeToTray)
            {
                Hide(); // Esconde da barra de tarefas se minimizado
            }
        }

        #endregion
    }
}
