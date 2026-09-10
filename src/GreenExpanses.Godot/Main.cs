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
    private Label _soilAnalysisLabel = null!;
    private VBoxContainer _fieldRows = null!;
    private OptionButton _cropChoice = null!;
    private Button _planCropButton = null!;
    private Button _soilAnalysisButton = null!;
    private Label _operationPreview = null!;
    private Button _scheduleOperationButton = null!;

    public override void _Ready()
    {
        try
        {
            _autosave = new AutosaveStore(ProjectSettings.GlobalizePath("user://saves"));
            BuildUi();
            try { _profile = FirstPlayableProfile.Load(); }
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
        var center = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        center.AddThemeConstantOverride("separation", 10);
        body.AddChild(center);
        var todayTitle = new Label { Text = "СЕГОДНЯ" };
        todayTitle.AddThemeFontSizeOverride("font_size", 20);
        center.AddChild(todayTitle);
        _todayLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = "Выберите собственное поле и начните сезон с анализа почвы." };
        center.AddChild(_todayLabel);
        center.AddChild(new HSeparator());
        var fieldsTitle = new Label { Text = "ПОЛЯ ХОЗЯЙСТВА И РАЙОНА" };
        fieldsTitle.AddThemeFontSizeOverride("font_size", 18);
        center.AddChild(fieldsTitle);
        var hint = new Label { Text = "Нажми на поле, чтобы открыть карточку. Точные агрохимические показатели чужих полей не раскрываются в общем списке.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        center.AddChild(hint);
        var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        center.AddChild(scroll);
        _fieldRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _fieldRows.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(_fieldRows);
    }

    private void BuildRightColumn(HBoxContainer body)
    {
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0) };
        right.AddThemeConstantOverride("separation", 8);
        body.AddChild(right);

        var attentionTitle = new Label { Text = "ТРЕБУЕТ ВНИМАНИЯ" };
        attentionTitle.AddThemeFontSizeOverride("font_size", 18);
        right.AddChild(attentionTitle);
        _attentionLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = "АНАЛИЗ — начните с анализа почвы на собственном поле." };
        right.AddChild(_attentionLabel);
        right.AddChild(new HSeparator());

        var fieldTitle = new Label { Text = "КАРТОЧКА ПОЛЯ" };
        fieldTitle.AddThemeFontSizeOverride("font_size", 18);
        right.AddChild(fieldTitle);
        _fieldDetail = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = "Выберите поле из списка." };
        right.AddChild(_fieldDetail);

        _soilAnalysisLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = "Анализ почвы: выберите собственное поле." };
        right.AddChild(_soilAnalysisLabel);
        _soilAnalysisButton = new Button { Text = "Заказать анализ почвы", Disabled = true, CustomMinimumSize = new Vector2(0, 38) };
        _soilAnalysisButton.Pressed += OrderSoilAnalysis;
        right.AddChild(_soilAnalysisButton);
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
        _planCropButton = new Button { Text = "Назначить культуру", Disabled = true, CustomMinimumSize = new Vector2(0, 38) };
        _planCropButton.Pressed += PlanSelectedCrop;
        right.AddChild(_planCropButton);

        right.AddChild(new HSeparator());
        var operationTitle = new Label { Text = "ПЕРВАЯ ОПЕРАЦИЯ: ПОСЕВ" };
        operationTitle.AddThemeFontSizeOverride("font_size", 18);
        right.AddChild(operationTitle);
        _operationPreview = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = "Сначала выберите собственное поле и культуру." };
        right.AddChild(_operationPreview);
        _scheduleOperationButton = new Button { Text = "Подтвердить операцию", Disabled = true, CustomMinimumSize = new Vector2(0, 40) };
        _scheduleOperationButton.Pressed += ScheduleSowing;
        right.AddChild(_scheduleOperationButton);
        right.AddChild(new Label
        {
            Text = "Расчёт производительности: P = W × V × Kₑ / 10. Ставка ₽/ч пока prototype-параметр из config и будет откалибрована отдельно.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
    }

    private void AddNavButton(Container parent, string text, bool active = false)
    {
        var button = new Button { Text = active ? $"● {text}" : text, CustomMinimumSize = new Vector2(150, 34) };
        button.Pressed += active ? () => SetStatus("Открыта главная панель хозяйства.") : () => SetStatus($"Раздел «{text}» пока не подключён к симуляции.");
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
        _state = InheritanceScenarioFactory.Create(seed, new Money(_profile.StartingCashRub), _profile.StarterFieldTargetAreaHa, _profile.StarterFieldMaxDistanceKm);
        _selectedField = null;
        SetStatus($"Создано хозяйство «Наследство», seed {seed}.");
        Refresh();
    }

    private void Advance(int days)
    {
        if (_state is null) return;
        EmptyDailySimulation.Advance(_state, days);
        SetStatus($"Календарь продвинут на {days} дн.");
        Refresh();
    }

    private void Save()
    {
        if (_state is null || _autosave is null) return;
        _autosave.Save(_state, _configVersion, "internal-first-playable-v4");
        SetStatus("Игра сохранена.");
    }

    private void Load()
    {
        if (_autosave is null) return;
        try
        {
            _state = _autosave.Load().State;
            _selectedField = null;
            SetStatus("Сохранение загружено.");
            Refresh();
        }
        catch (FileNotFoundException) { SetStatus("Сохранение пока не создано."); }
        catch (InvalidDataException exception) { SetStatus($"Ошибка сохранения: {exception.Message}"); }
    }

    private void Refresh()
    {
        if (_state is null) return;
        var view = FirstPlayableProjectionFactory.Build(_state, 30);
        _dateLabel.Text = $"Дата: {view.CurrentDateTime.Value:dd.MM.yyyy}";
        _cashLabel.Text = $"Деньги: {view.CashRub:N0} ₽";

        var ownedFieldId = _state.Farm.OwnedFieldIds.FirstOrDefault();
        var ownedPlan = _state.Farm.CropPlans.FirstOrDefault(plan => plan.FieldId == ownedFieldId);
        var ownedOperation = _state.Farm.Operations.FirstOrDefault(operation => operation.FieldId == ownedFieldId);
        var ownedAnalysis = LatestSoilAnalysis(ownedFieldId);
        _capacityLabel.Text = ownedOperation is null ? "Capacity Forecast: не рассчитан" : $"Посев: {ownedOperation.RequiredHours:N1} ч";
        _todayLabel.Text = TodayText(view, ownedPlan, ownedOperation, ownedAnalysis);
        _attentionLabel.Text = AttentionText(ownedPlan, ownedOperation, ownedAnalysis);

        foreach (var child in _fieldRows.GetChildren()) child.QueueFree();
        foreach (var field in view.Fields)
        {
            var owned = field.IsOwned ? "СВОЁ" : "РАЙОН";
            var plan = _state.Farm.CropPlans.FirstOrDefault(item => item.FieldId == field.FieldId);
            var operation = _state.Farm.Operations.FirstOrDefault(item => item.FieldId == field.FieldId);
            var analysis = LatestSoilAnalysis(field.FieldId);
            var planText = plan is null ? string.Empty : $"  •  {CropName(plan.CropId)}";
            var operationText = operation is null ? string.Empty : "  •  посев запланирован";
            var analysisText = analysis is null ? string.Empty : analysis.Status == new CatalogId("completed") ? "  •  анализ готов" : "  •  анализ в работе";
            var button = new Button
            {
                Text = field.IsOwned
                    ? $"[{owned}] {field.AreaHa:N1} га  •  {field.DistanceKm:N1} км  •  плодородие {field.Fertility:N0}{analysisText}{planText}{operationText}"
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
            if (match is not null) SelectField(match);
        }
        else
        {
            _soilAnalysisButton.Disabled = true;
            _planCropButton.Disabled = true;
            _scheduleOperationButton.Disabled = true;
            _operationPreview.Text = "Сначала выберите собственное поле.";
            _soilAnalysisLabel.Text = "Анализ почвы: выберите собственное поле.";
        }
    }

    private string TodayText(FirstPlayableView view, FieldCropPlan? ownedPlan, FieldOperationPlan? ownedOperation, SoilAnalysisOrder? ownedAnalysis)
    {
        if (ownedAnalysis is null) return $"Хозяйство «Наследство»: {view.OwnedAreaHa:N1} га. Первое решение — заказать анализ почвы.";
        if (ownedAnalysis.Status != new CatalogId("completed")) return $"Анализ почвы в работе. Осталось дней: {RemainingDays(ownedAnalysis)}.";
        if (ownedPlan is null) return "Анализ почвы готов. Теперь выберите культуру сезона.";
        if (ownedOperation is null) return $"План сезона: {CropName(ownedPlan.CropId)}. Рассчитайте и подтвердите первую операцию — посев.";
        return $"Посев запланирован: {CropName(ownedOperation.CropId)}, {ownedOperation.RemainingArea.Value:N1} га, {ownedOperation.RequiredHours:N1} ч.";
    }

    private string AttentionText(FieldCropPlan? ownedPlan, FieldOperationPlan? ownedOperation, SoilAnalysisOrder? ownedAnalysis)
    {
        if (ownedAnalysis is null) return $"АНАЛИЗ — закажите анализ почвы: {_profile.PrototypeSoilAnalysisCostRub:N0} ₽, {_profile.PrototypeSoilAnalysisDurationDays} дн.";
        if (ownedAnalysis.Status != new CatalogId("completed")) return $"АНАЛИЗ — лаборатория работает, осталось дней: {RemainingDays(ownedAnalysis)}.";
        if (ownedPlan is null) return "ПЛАН — анализ готов, но культура сезона ещё не выбрана.";
        if (ownedOperation is null) return "ОПЕРАЦИЯ — посев рассчитан, но ещё не подтверждён.";
        return $"Посев: статус «запланировано», оценочная стоимость {ownedOperation.EstimatedCost.Value:N0} ₽.";
    }

    private void SelectField(FirstPlayableFieldView field)
    {
        _selectedField = field;
        var ownership = field.IsOwned ? "Собственное поле" : "Поле района";
        var analysis = _state is null ? null : LatestSoilAnalysis(field.FieldId);
        var chemistry = field.IsOwned && analysis?.Status == new CatalogId("completed")
            ? $"pH: {field.Ph:N2}\nN / P / K: {field.SoilN:N1} / {field.SoilP:N1} / {field.SoilK:N1}\nОрганическое вещество: {field.OrganicMatter:N1}"
            : field.IsOwned
                ? "Агрохимия: точные N/P/K и pH появятся после завершения анализа почвы."
                : "Агрохимия: чужие поля требуют отдельного обследования перед раскрытием точных показателей.";

        _fieldDetail.Text =
            $"{ownership}\n\n" +
            $"Площадь: {field.AreaHa:N1} га\n" +
            $"Расстояние от базы: {field.DistanceKm:N1} км\n" +
            $"Плодородие: {field.Fertility:N1}/100\n" +
            $"Дренаж: {TranslateDrainage(field.Drainage)}\n\n" + chemistry;

        UpdateSoilAnalysisControls(field, analysis);
        UpdatePlanningControls(field);
        SetStatus($"Открыта карточка поля {field.FieldId}.");
    }

    private void UpdateSoilAnalysisControls(FirstPlayableFieldView field, SoilAnalysisOrder? analysis)
    {
        if (!field.IsOwned)
        {
            _soilAnalysisLabel.Text = "Анализ почвы: доступен только для собственных полей.";
            _soilAnalysisButton.Disabled = true;
            return;
        }

        if (analysis is null)
        {
            _soilAnalysisLabel.Text = $"Анализ почвы: не заказан. Стоимость {_profile.PrototypeSoilAnalysisCostRub:N0} ₽, срок {_profile.PrototypeSoilAnalysisDurationDays} дн.";
            _soilAnalysisButton.Text = "Заказать анализ почвы";
            _soilAnalysisButton.Disabled = false;
            return;
        }

        if (analysis.Status == new CatalogId("completed"))
        {
            _soilAnalysisLabel.Text = "Анализ почвы: готов. Точные показатели открыты в карточке поля.";
            _soilAnalysisButton.Text = "Анализ готов";
            _soilAnalysisButton.Disabled = true;
            return;
        }

        _soilAnalysisLabel.Text = $"Анализ почвы: выполняется. Осталось дней: {RemainingDays(analysis)}.";
        _soilAnalysisButton.Text = "Анализ в работе";
        _soilAnalysisButton.Disabled = true;
    }

    private void UpdatePlanningControls(FirstPlayableFieldView field)
    {
        if (_state is null || !field.IsOwned)
        {
            _planCropButton.Disabled = true;
            _scheduleOperationButton.Disabled = true;
            _operationPreview.Text = "Сначала выберите собственное поле.";
            return;
        }

        var analysisReady = LatestSoilAnalysis(field.FieldId)?.Status == new CatalogId("completed");
        var cropPlan = _state.Farm.CropPlans.FirstOrDefault(plan => plan.FieldId == field.FieldId);
        var operation = _state.Farm.Operations.FirstOrDefault(plan => plan.FieldId == field.FieldId && plan.OperationType == new CatalogId("sowing"));
        _planCropButton.Disabled = !analysisReady;
        if (!analysisReady)
        {
            _operationPreview.Text = "Сначала дождитесь анализа почвы, затем выберите культуру.";
            _scheduleOperationButton.Disabled = true;
            return;
        }

        if (cropPlan is null)
        {
            _operationPreview.Text = "Анализ готов. Выберите культуру сезона.";
            _scheduleOperationButton.Disabled = true;
            return;
        }

        var requiredHours = field.AreaHa / _profile.PrototypeSowingProductivityHaPerHour;
        var cost = requiredHours * _profile.PrototypeSowingOperatingCostRubPerHour;
        _operationPreview.Text = operation is null
            ? $"Культура: {CropName(cropPlan.CropId)}\nПроизводительность: {_profile.PrototypeSowingProductivityHaPerHour:N2} га/ч\nВремя: {requiredHours:N2} ч\nОценочная стоимость: {cost:N0} ₽"
            : $"Посев уже запланирован: {operation.RequiredHours:N2} ч, {operation.EstimatedCost.Value:N0} ₽.";
        _scheduleOperationButton.Disabled = operation is not null;
    }

    private void OrderSoilAnalysis()
    {
        if (_state is null || _selectedField is null) return;
        var command = new OrderSoilAnalysisCommand(
            EntityId.New(),
            EntityId.New(),
            null,
            _selectedField.FieldId,
            new Money(_profile.PrototypeSoilAnalysisCostRub),
            _profile.PrototypeSoilAnalysisDurationDays);
        var result = _commandBus.Execute(_state, command);
        SetStatus(result.Succeeded ? "Анализ почвы заказан. Деньги списаны, ждём лабораторию." : $"Анализ не заказан: {result.Code}");
        Refresh();
    }

    private void PlanSelectedCrop()
    {
        if (_state is null || _selectedField is null) return;
        var result = _commandBus.Execute(_state, new PlanCropCommand(EntityId.New(), EntityId.New(), null, _selectedField.FieldId, SelectedCropId()));
        SetStatus(result.Succeeded ? $"Культура назначена: {CropName(SelectedCropId())}." : $"Культура не назначена: {result.Code}");
        Refresh();
    }

    private void ScheduleSowing()
    {
        if (_state is null || _selectedField is null) return;
        var requiredHours = _selectedField.AreaHa / _profile.PrototypeSowingProductivityHaPerHour;
        var estimatedCost = decimal.Round(requiredHours * _profile.PrototypeSowingOperatingCostRubPerHour, 2, MidpointRounding.AwayFromZero);
        var result = _commandBus.Execute(_state, new ScheduleSowingOperationCommand(
            EntityId.New(),
            EntityId.New(),
            null,
            _selectedField.FieldId,
            _profile.PrototypeSowingProductivityHaPerHour,
            new Money(estimatedCost)));
        SetStatus(result.Succeeded ? "Посев запланирован, оценочная стоимость зарезервирована." : $"Операция не подтверждена: {result.Code}");
        Refresh();
    }

    private SoilAnalysisOrder? LatestSoilAnalysis(EntityId fieldId)
    {
        return _state?.Farm.SoilAnalysisOrders.LastOrDefault(order => order.FieldId == fieldId);
    }

    private int RemainingDays(SoilAnalysisOrder order)
    {
        if (_state is null) return 0;
        return Math.Max(0, (order.DueAt.Value.Date - _state.CurrentDateTime.Value.Date).Days);
    }

    private CatalogId SelectedCropId() => _cropChoice.Selected switch
    {
        1 => new CatalogId("grain_corn"),
        2 => new CatalogId("sunflower"),
        3 => new CatalogId("soybean"),
        _ => new CatalogId("winter_wheat")
    };

    private static string CropName(CatalogId cropId) => cropId.Value switch
    {
        "winter_wheat" => "озимая пшеница",
        "grain_corn" => "кукуруза",
        "sunflower" => "подсолнечник",
        "soybean" => "соя",
        _ => cropId.Value
    };

    private void SetStatus(string text)
    {
        if (_status is not null) _status.Text = text;
    }

    private void ShowStartupError(Exception exception)
    {
        try
        {
            foreach (var child in GetChildren()) child.QueueFree();
            var label = new Label
            {
                Text = "Не удалось запустить прототип.\n\n" + exception.GetType().Name + ": " + exception.Message + "\n\nПодробности записаны в user://startup_error.log",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            label.OffsetLeft = 32;
            label.OffsetTop = 32;
            label.OffsetRight = -32;
            label.OffsetBottom = -32;
            AddChild(label);
            File.WriteAllText(ProjectSettings.GlobalizePath("user://startup_error.log"), exception.ToString());
        }
        catch (Exception loggingException)
        {
            GD.PushError($"Could not render/write startup error: {loggingException}");
        }
    }

    private static string TranslateDrainage(string drainage) => drainage switch
    {
        "normal" => "нормальный",
        "dry_prone" => "склонность к пересыханию",
        "wet_prone" => "склонность к переувлажнению",
        "problematic" => "проблемный",
        _ => drainage
    };
}
