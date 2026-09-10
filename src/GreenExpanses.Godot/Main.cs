using System.Globalization;
using Godot;
using GreenExpanses.Application;
using GreenExpanses.Domain;
using GreenExpanses.Persistence;
using GreenExpanses.Simulation;

namespace GreenExpanses.GodotClient;

public partial class Main : Control
{
    private readonly ConfigVersion _configVersion = new("1.0.0-dev");
    private FirstPlayableProfile _profile = FirstPlayableProfile.Default;
    private GameState? _state;
    private AutosaveStore? _autosave;
    private LineEdit _seedInput = null!;
    private Label _status = null!;
    private Label _summary = null!;
    private VBoxContainer _fieldRows = null!;

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

    private void BuildUi()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        var title = new Label { Text = "ЗЕЛЁНЫЕ ПРОСТОРЫ  •  INTERNAL FIRST PLAYABLE" };
        title.AddThemeFontSizeOverride("font_size", 26);
        root.AddChild(title);

        _status = new Label { Text = "Готово" };
        root.AddChild(_status);

        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 8);
        root.AddChild(toolbar);

        toolbar.AddChild(new Label { Text = "Seed:" });
        _seedInput = new LineEdit { Text = "20260910", CustomMinimumSize = new Vector2(150, 0) };
        toolbar.AddChild(_seedInput);
        AddButton(toolbar, "Новая игра", NewGame);
        AddButton(toolbar, "+1 день", () => Advance(1));
        AddButton(toolbar, "+7 дней", () => Advance(7));
        AddButton(toolbar, "Сохранить", Save);
        AddButton(toolbar, "Загрузить", Load);

        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_summary);
        root.AddChild(new HSeparator());

        var fieldsTitle = new Label { Text = "ПОЛЯ  •  сначала собственное, затем ближайшие" };
        fieldsTitle.AddThemeFontSizeOverride("font_size", 18);
        root.AddChild(fieldsTitle);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(scroll);
        _fieldRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _fieldRows.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_fieldRows);
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
        SetStatus($"Создано «Наследство», seed {seed}.");
        Refresh();
    }

    private void Advance(int days)
    {
        if (_state is null)
        {
            return;
        }

        EmptyDailySimulation.Advance(_state, days);
        SetStatus($"Прошло дней: {days}.");
        Refresh();
    }

    private void Save()
    {
        if (_state is null || _autosave is null)
        {
            return;
        }

        _autosave.Save(_state, _configVersion, "internal-first-playable-v0");
        SetStatus("Автосохранение записано.");
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
        _summary.Text = $"Дата: {view.CurrentDateTime.Value:dd.MM.yyyy}    |    День: {_state.Simulation.CompletedDays}    |    Деньги: {view.CashRub:N0} ₽    |    Собственная площадь: {view.OwnedAreaHa:N1} га    |    Полей в районе: {view.WorldFieldCount}";

        foreach (var child in _fieldRows.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var field in view.Fields)
        {
            var owned = field.IsOwned ? "[СВОЁ]" : "[район]";
            _fieldRows.AddChild(new Label
            {
                Text = $"{owned}  {field.AreaHa,6:N1} га  •  {field.DistanceKm,5:N1} км  •  плодородие {field.Fertility,5:N1}  •  pH {field.Ph:N2}  •  N/P/K {field.SoilN:N1}/{field.SoilP:N1}/{field.SoilK:N1}  •  OM {field.OrganicMatter:N1}  •  дренаж: {TranslateDrainage(field.Drainage)}"
            });
        }
    }

    private void SetStatus(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
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
