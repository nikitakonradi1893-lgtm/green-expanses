using System.Globalization;
using Godot;
using GreenExpanses.Application;
using GreenExpanses.Application.Commands;
using GreenExpanses.Domain;
using GreenExpanses.Persistence;
using GreenExpanses.Simulation;

namespace GreenExpanses.GodotClient;

public partial class Main : Control
{
    private readonly ConfigVersion _configVersion = new("1.0.0-dev");
    private readonly CommandBus _commandBus = FirstPlayableCommandBusFactory.Create();
    private FirstPlayableProfile _profile = FirstPlayableProfile.Default;
    private GameState? _state;
    private AutosaveStore? _autosave;
    private FirstPlayableFieldView? _selectedField;

    private LineEdit _seedInput = null!;
    private Label _status = null!;
    private Label _dateLabel = null!;
    private Label _cashLabel = null!;
    private Label _capacityLabel = null!;
    private Label _todayLabel = null!;
    private Label _attentionLabel = null!;
    private Label _fieldDetail = null!;
    private VBoxContainer _fieldRows = null!;
    private OptionButton _cropChoice = null!;
    private Button _planCropButton = null!;

    public override void _Ready()
    {
        try
        {
            _autosave = new AutosaveStore(ProjectSettings.GlobalizePath("user://saves"));
            BuildUi();

            try
            {
                _profile = FirstPlayableProfile.Load();
            }
            catch (Exception exception)
            {
                GD.PushWarning($"Could not load first playable profile, using safe defaults: {exception}");
                _profile = FirstPlayableProfile.Default;
            }

            NewGame();
        }
        catch (Exception exception)
        {
            GD.PushError($"Fatal startup error: {exception}");
            ShowStartupError(exception);
        }
    }

    private void BuildUi()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 18);
        root.AddChild(header);

        var title = new Label { Text = "ЗЕЛЁНЫЕ ПРОСТОРЫ" };
        title.AddThemeFontSizeOverride("font_size", 26);
        header.AddChild(title);

        _dateLabel = new Label { Text = "Дата: —" };
        _cashLabel = new Label { Text = "Деньги: —" };
        _capacityLabel = new Label { Text = "Capacity Forecast: не рассчитан" };
        header.AddChild(_dateLabel);
        header.AddChild(_cashLabel);
        header.AddChild(_capacityLabel);

        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 8);
        root.AddChild(toolbar);

        toolbar.AddChild(new Label { Text = "Seed" });
        _seedInput = new LineEdit { Text = "20260910", CustomMinimumSize = new Vector2(130, 0) };
        toolbar.AddChild(_seedInput);
        AddButton(toolbar, "Новая игра", NewGame);
        AddButton(toolbar, "+1 день", () => Advance(1));
        AddButton(toolbar, "+7 дней", () => Advance(7));
        AddButton(toolbar, "Сохранить", Save);
        AddButton(toolbar, "Загрузить", Load);

        _status = new Label { Text = "Готово" };
        root.AddChild(_status);
        root.AddChild(new HSeparator());

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 16);
        root.AddChild(body);

        BuildNavigation(body);
        BuildCenter(body);
        BuildRightColumn(body);
    }

    private void BuildNavigation(HBoxContainer body)
    {
        var nav = new VBoxContainer { CustomMinimumSize = new Vector2(165, 0) };
        nav.AddThemeConstantOverride("separation", 6);
        body.AddChild(nav);

        var label = new Label { Text = "РАЗДЕЛЫ" };
        label.AddThemeFontSizeOverride("font_size", 15);
        nav.AddChild(label);

        AddNavButton(nav, "Главная", true);
        AddNavButton(nav, "Поля");
        AddNavButton(nav, "Планировщик");
        AddNavButton(nav, "Техника");
        AddNavButton(nav, "Склад");
        AddNavButton(nav, "Рынок");
        AddNavButton(nav, "Финансы");
        AddNavButton(nav, "Работники");
        AddNavButton(nav, "Отчёты");
    }

    private void BuildCenter(HBoxContainer body)
    {
        var center = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        center.AddThemeConstantOverride("separation", 10);
        body.AddChild(center);

        var todayTitle = new Label { Text = "СЕГОДНЯ" };
        todayTitle.AddThemeFontSizeOverride("font_size", 20);
        center.AddChild(todayTitle);

        _todayLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = "Выберите собственное поле и назначьте культуру сезона."
        };
        center.AddChild(_todayLabel);

        center.AddChild(new HSeparator());

        var fieldsTitle = new Label { Text = "ПОЛЯ ХОЗЯЙСТВА И РАЙОНА" };
        fieldsTitle.AddThemeFontSizeOverride("font_size", 18);
        center.AddChild(fieldsTitle);

        var hint = new Label
        {
            Text = "Нажми на поле, чтобы открыть карточку. Точные агрохимические показатели чужих полей не раскрываются в общем списке."
        };
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        center.AddChild(hint);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        center.AddChild(scroll);

        _fieldRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _fieldRows.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(_fieldRows);
    }

    private void BuildRightColumn(HBoxContainer body)
    {
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(350, 0) };
        right.AddThemeConstantOverride("separation", 10);
        body.AddChild(right);

        var attentionTitle = new Label { Text = "ТРЕБУЕТ ВНИМАНИЯ" };
        attentionTitle.AddThemeFontSizeOverride("font_size", 18);
        right.AddChild(attentionTitle);

        _attentionLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = "ПЛАН — для собственного поля ещё не выбрана культура сезона."
        };
        right.AddChild(_attentionLabel);

        right.AddChild(new HSeparator());

        var fieldTitle = new Label { Text = "КАРТОЧКА ПОЛЯ" };
        fieldTitle.AddThemeFontSizeOverride("font_size", 18);
        right.AddChild(fieldTitle);

        _fieldDetail = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = "Выберите поле из списка."
        };
        right.AddChild(_fieldDetail);

        right.AddChild(new HSeparator());

        var planTitle = new Label { Text = "ПЛАН СЕЗОНА" };
        planTitle.AddThemeFontSizeOverride("font_size", 18);
        right.AddChild(planTitle);

        _cropChoice = new OptionButton();
        _cropChoice.AddItem("Озимая пшеница");
        _cropChoice.AddItem("Кукуруза");
        _cropChoice.AddItem("Подсолнечник");
        _cropChoice.AddItem("Соя");
        right.AddChild(_cropChoice);

        _planCropButton = new Button
        {
            Text = "Назначить культуру",
            Disabled = true,
            CustomMinimumSize = new Vector2(0, 40)
        };
        _planCropButton.Pressed += PlanSelectedCrop;
        right.AddChild(_planCropButton);

        var planHint = new Label
        {
            Text = "Это уже игровое решение: выбор записывается в GameState и EventLog и сохраняется вместе с кампанией.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        right.AddChild(planHint);
    }

    private void AddNavButton(Container parent, string text, bool active = false)
    {
        var button = new Button
        {
            Text = active ? $"● {text}" : text,
            CustomMinimumSize = new Vector2(150, 34)
        };

        if (!active)
        {
            button.Pressed += () => SetStatus($"Раздел «{text}» пока не подключён к симуляции.");
        }
        else
        {
            button.Pressed += () => SetStatus("Открыта главная панель хозяйства.");
        }

        parent.AddChild(button);
    }

    private static void AddButton(Container parent, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        parent.AddChild(button);
    }

    private void NewGame()
    {
        if (!ulong.TryParse(_seedInput?.Text ?? "20260910", NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
        {
            SetStatus("Seed должен быть целым неотрицательным числом.");
            return;
        }

        _state = InheritanceScenarioFactory.Create(
            seed,
            new Money(_profile.StartingCashRub),
            _profile.StarterFieldTargetAreaHa,
            _profile.StarterFieldMaxDistanceKm);
        _selectedField = null;
        SetStatus($"Создано хозяйство «Наследство», seed {seed}.");
        Refresh();
    }

    private void Advance(int days)
    {
        if (_state is null)
        {
            return;
        }

        EmptyDailySimulation.Advance(_state, days);
        SetStatus($"Календарь продвинут на {days} дн.");
        Refresh();
    }

    private void Save()
    {
        if (_state is null || _autosave is null)
        {
            return;
        }

        _autosave.Save(_state, _configVersion, "internal-first-playable-v2");
        SetStatus("Игра сохранена.");
    }

    private void Load()
    {
        if (_autosave is null)
        {
            return;
        }

        try
        {
            _state = _autosave.Load().State;
            _selectedField = null;
            SetStatus("Сохранение загружено.");
            Refresh();
        }
        catch (FileNotFoundException)
        {
            SetStatus("Сохранение пока не создано.");
        }
        catch (InvalidDataException exception)
        {
            SetStatus($"Ошибка сохранения: {exception.Message}");
        }
    }

    private void Refresh()
    {
        if (_state is null)
        {
            return;
        }

        var view = FirstPlayableProjectionFactory.Build(_state, 30);
        _dateLabel.Text = $"Дата: {view.CurrentDateTime.Value:dd.MM.yyyy}";
        _cashLabel.Text = $"Деньги: {view.CashRub:N0} ₽";
        _capacityLabel.Text = "Capacity Forecast: не рассчитан";

        var ownedPlan = _state.Farm.CropPlans.FirstOrDefault(plan => _state.Farm.OwnedFieldIds.Contains(plan.FieldId));
        _todayLabel.Text = ownedPlan is null
            ? $"Хозяйство «Наследство»: {view.OwnedAreaHa:N1} га. Первое решение сезона — выбрать культуру для собственного поля."
            : $"Хозяйство «Наследство»: {view.OwnedAreaHa:N1} га. План сезона: {CropName(ownedPlan.CropId)}. Следующий шаг — сформировать первую полевую операцию.";
        _attentionLabel.Text = ownedPlan is null
            ? "ПЛАН — для собственного поля ещё не выбрана культура сезона."
            : $"План культуры принят: {CropName(ownedPlan.CropId)}. Операции и техника будут подключены следующим срезом.";

        foreach (var child in _fieldRows.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var field in view.Fields)
        {
            var owned = field.IsOwned ? "СВОЁ" : "РАЙОН";
            var plan = _state.Farm.CropPlans.FirstOrDefault(item => item.FieldId == field.FieldId);
            var planText = plan is null ? string.Empty : $"  •  план: {CropName(plan.CropId)}";
            var button = new Button
            {
                Text = field.IsOwned
                    ? $"[{owned}] {field.AreaHa:N1} га  •  {field.DistanceKm:N1} км  •  плодородие {field.Fertility:N0}  •  дренаж: {TranslateDrainage(field.Drainage)}{planText}"
                    : $"[{owned}] {field.AreaHa:N1} га  •  {field.DistanceKm:N1} км  •  качество {field.Fertility:N0}  •  дренаж: {TranslateDrainage(field.Drainage)}",
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 38)
            };
            var selected = field;
            button.Pressed += () => SelectField(selected);
            _fieldRows.AddChild(button);
        }

        if (_selectedField is not null)
        {
            var match = view.Fields.FirstOrDefault(field => field.FieldId == _selectedField.FieldId);
            if (match is not null)
            {
                SelectField(match);
            }
        }
        else
        {
            _planCropButton.Disabled = true;
        }
    }

    private void SelectField(FirstPlayableFieldView field)
    {
        _selectedField = field;
        var ownership = field.IsOwned ? "Собственное поле" : "Поле района";
        var chemistry = field.IsOwned
            ? $"Стартовое обследование\npH: {field.Ph:N2}\nN / P / K: {field.SoilN:N1} / {field.SoilP:N1} / {field.SoilK:N1}\nОрганическое вещество: {field.OrganicMatter:N1}"
            : "Агрохимия: точные N/P/K и pH должны раскрываться после анализа почвы. Механика анализа пока не подключена.";
        var plan = _state?.Farm.CropPlans.FirstOrDefault(item => item.FieldId == field.FieldId);
        var planText = plan is null ? "не назначена" : CropName(plan.CropId);

        _fieldDetail.Text =
            $"{ownership}\n\n" +
            $"Площадь: {field.AreaHa:N1} га\n" +
            $"Расстояние от базы: {field.DistanceKm:N1} км\n" +
            $"Плодородие: {field.Fertility:N1}/100\n" +
            $"Дренаж: {TranslateDrainage(field.Drainage)}\n" +
            $"Культура сезона: {planText}\n\n" +
            chemistry;

        _planCropButton.Disabled = !field.IsOwned;
        SetStatus(field.IsOwned
            ? "Собственное поле выбрано. Можно назначить культуру сезона."
            : "Поле района выбрано. Планировать культуру можно только на собственных полях.");
    }

    private void PlanSelectedCrop()
    {
        if (_state is null || _selectedField is null)
        {
            SetStatus("Сначала выберите собственное поле.");
            return;
        }

        var cropId = _cropChoice.Selected switch
        {
            0 => new CatalogId("winter_wheat"),
            1 => new CatalogId("corn"),
            2 => new CatalogId("sunflower"),
            3 => new CatalogId("soy"),
            _ => new CatalogId("winter_wheat")
        };

        var command = new PlanCropCommand(
            EntityId.New(),
            EntityId.New(),
            null,
            _selectedField.FieldId,
            cropId);
        var result = _commandBus.Execute(_state, command);

        if (!result.Succeeded)
        {
            SetStatus($"Не удалось назначить культуру: {result.Code}.");
            return;
        }

        SetStatus($"План принят: {CropName(cropId)}. Решение записано в кампанию.");
        Refresh();
    }

    private void SetStatus(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    }

    private void ShowStartupError(Exception exception)
    {
        try
        {
            foreach (var child in GetChildren())
            {
                child.QueueFree();
            }

            var label = new Label
            {
                Text = "Не удалось запустить прототип.\n\n" + exception.GetType().Name + ": " + exception.Message +
                       "\n\nПодробности записаны в user://startup_error.log",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            label.OffsetLeft = 32;
            label.OffsetTop = 32;
            label.OffsetRight = -32;
            label.OffsetBottom = -32;
            AddChild(label);

            var path = ProjectSettings.GlobalizePath("user://startup_error.log");
            File.WriteAllText(path, exception.ToString());
        }
        catch (Exception loggingException)
        {
            GD.PushError($"Could not render/write startup error: {loggingException}");
        }
    }

    private static string CropName(CatalogId cropId) => cropId.Value switch
    {
        "winter_wheat" => "озимая пшеница",
        "corn" => "кукуруза",
        "sunflower" => "подсолнечник",
        "soy" => "соя",
        _ => cropId.Value
    };

    private static string TranslateDrainage(string drainage) => drainage switch
    {
        "normal" => "нормальный",
        "dry_prone" => "склонность к пересыханию",
        "wet_prone" => "склонность к переувлажнению",
        "problematic" => "проблемный",
        _ => drainage
    };
}
