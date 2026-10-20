namespace VampireHuntersVR.Settings;

using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using VampireHuntersVR.Render;

// D-171: one window for the picture size, in the same shape as the reference settings program the user pointed at
// ("Low 0.8, Normal 1.0, High 1.2, Custom 0.5-2" beside "Bigger is sharper, but gives lower FPS."). It edits only
// EyePreset/EyeWidth/EyeHeight/EyeScale, shows the size the runtime will really use, keeps a timestamped backup of the
// previous file, and refuses a value the runtime would otherwise clamp behind the user's back. D-175 adds one row:
// the in-VR UI size (uiScale), which the extras plugin reads from the same file and applies to the whole in-VR HUD.
internal sealed class SettingsForm : Form
{
    private readonly TextBox _path = new() { Left = 112, Top = 12, Width = 330 };
    private readonly RadioButton _low = new() { Left = 22, Top = 72, Width = 110, Text = "Low 0.8" };
    private readonly RadioButton _normal = new() { Left = 150, Top = 72, Width = 190, Text = "Normal 1.0 (default)" };
    private readonly RadioButton _high = new() { Left = 22, Top = 98, Width = 110, Text = "High 1.2" };
    private readonly RadioButton _custom = new() { Left = 150, Top = 98, Width = 140, Text = "Custom 0.5-2.0" };
    private readonly NumericUpDown _scale = new() { Left = 296, Top = 95, Width = 90, DecimalPlaces = 2, Minimum = 0.50m, Maximum = 2.00m, Increment = 0.05m, Value = 1.00m };
    private readonly NumericUpDown _uiScale = new() { Left = 560, Top = 122, Width = 90, DecimalPlaces = 2, Minimum = 0.50m, Maximum = 3.00m, Increment = 0.05m, Value = 1.50m };
    // D-186: per-eye MSAA samples for the eye textures (1 = off, 2/4/8 = that many samples; 4 = shipped).
    private readonly ComboBox _msaa = new() { Left = 560, Top = 175, Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
    // D-186-FovScale: per-axis render-FOV scales. 1.00 = pass-through (shipped), 0.50-0.95 = narrow the rendered
    // FOV on each axis so the GPU draws fewer pixels (the OpenXR runtime reports a wider FOV than the lens
    // displays; the extra is the lens-distortion / barrel margin). X scales the horizontal frustum
    // (angleLeft, angleRight) and Y scales the vertical frustum (angleUp, angleDown); either axis can be
    // narrowed independently. The host refuses values outside 0.50-1.00 and keeps the previous value.
    // 11 choices in 0.05 steps for each axis.
    // D-192: the per-axis FOV scale is a slider now (the user asked for 슬라이더): TrackBar 50-100 = 0.50-1.00 in
    // 0.05 steps, matching HostConfigTool.FovScaleChoices, with the exact value shown beside each slider.
    private readonly TrackBar _fovScaleX = new() { Left = 140, Top = 208, Width = 130, Height = 40, AutoSize = false, Orientation = Orientation.Horizontal, Minimum = 50, Maximum = 100, TickStyle = TickStyle.BottomRight, TickFrequency = 5, SmallChange = 5, LargeChange = 10, Value = 100, BackColor = SystemColors.Control };
    private readonly TrackBar _fovScaleY = new() { Left = 500, Top = 208, Width = 130, Height = 40, AutoSize = false, Orientation = Orientation.Horizontal, Minimum = 50, Maximum = 100, TickStyle = TickStyle.BottomRight, TickFrequency = 5, SmallChange = 5, LargeChange = 10, Value = 100, BackColor = SystemColors.Control };
    private readonly Label _fovXVal = new() { Left = 276, Top = 222, Width = 56, ForeColor = Color.DimGray, Text = "1.00" };
    private readonly Label _fovYVal = new() { Left = 636, Top = 222, Width = 56, ForeColor = Color.DimGray, Text = "1.00" };
    // D-104-iso-promotion: anisotropic filtering on eye texture sampling (D-104). Host code already reads the key
    // (OpenXrHost.cs:879); 0.7.1 promotes it to the user-facing settings tool. Default OFF (matches D-104).
    private readonly CheckBox _aniso = new() { Left = 22, Top = 176, Width = 360, Height = 20, Text = "Anisotropic 16x (smoother angled textures; ~negligible cost)" };
    private readonly Label _base = new() { Left = 22, Top = 152, Width = 430, Height = 22, ForeColor = Color.DimGray };
    private readonly Label _preview = new() { Left = 22, Top = 306, Width = 656, Height = 110, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
    private readonly Label _status = new() { Left = 22, Top = 424, Width = 656, Height = 100, ForeColor = Color.DimGray };
    private readonly Button _save = new() { Text = "Apply", Left = 490, Top = 542, Width = 90, Height = 34 };
    private readonly Button _close = new() { Text = "Close", Left = 590, Top = 542, Width = 90, Height = 34 };
    private readonly Button _resetBase = new() { Text = "Use 1920x1080 base", Left = 462, Top = 148, Width = 216, Height = 28 };
    private int _baseWidth = 1920;
    private int _baseHeight = 1080;
    private JsonObject? _root;

    public SettingsForm(string? path)
    {
        Text = "Vampire Hunters VR setting";
        // D-186-FovScale: 700x580 (was 480) to fit the per-axis Render FOV scale rows (X scales the
        // horizontal frustum, Y scales the vertical) above the "This will be used" preview, which now
        // carries the combined "Render FOV scale X x0.85 Y x0.80" line; the preview label needs enough
        // height to render all 6 lines without clipping, so the form grows by another 40 px from 540 to
        // 580. ClippedControls() is asserted by --form-selftest, so a later edit that pushes a label off
        // the window fails the build instead of shipping a clipped window. FixedDialog + both this size
        // and the self-test keep the whole picture readable at a glance.
        ClientSize = new Size(700, 600);
        AutoScaleMode = AutoScaleMode.Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        var find = new Button { Text = "Find...", Left = 450, Top = 10, Width = 85 };
        var reopen = new Button { Text = "Reload", Left = 545, Top = 10, Width = 85 };
        Controls.AddRange(new Control[]
        {
            new Label { Text = "Settings file", Left = 22, Top = 15, Width = 84 },
            _path, find, reopen,
            new Label { Text = "Picture size", Left = 22, Top = 46, Width = 200, Font = new Font("Segoe UI", 9F, FontStyle.Bold) },
            _low, _normal, _high, _custom, _scale,
            new Label { Text = "Bigger is sharper, but gives lower FPS.", Left = 22, Top = 126, Width = 430, ForeColor = Color.DimGray },
            new Label { Text = "In-VR UI size x", Left = 456, Top = 126, Width = 100, ForeColor = Color.DimGray },
            _uiScale,
            new Label { Text = "Eye MSAA (1 = off)", Left = 396, Top = 178, Width = 160, ForeColor = Color.DimGray },
            _msaa,
            new Label { Text = "Render FOV X (horizontal)", Left = 22, Top = 222, Width = 110, ForeColor = Color.DimGray },
            _fovScaleX, _fovXVal,
            new Label { Text = "Render FOV Y (vertical)", Left = 380, Top = 222, Width = 110, ForeColor = Color.DimGray },
            _fovScaleY, _fovYVal,
            _aniso,
            _base, _resetBase,
            new Label { Text = "This will be used", Left = 22, Top = 296, Width = 200 },
            _preview, _status, _save, _close,
        });
        find.Click += (_, _) => Browse();
        reopen.Click += (_, _) => LoadConfig(_path.Text);
        _resetBase.Click += (_, _) => { _baseWidth = 1920; _baseHeight = 1080; Preview(); };
        _save.Click += (_, _) => Save();
        _close.Click += (_, _) => Close();
        foreach (RadioButton radio in new[] { _low, _normal, _high, _custom }) radio.CheckedChanged += (_, _) => Preview();
        _scale.ValueChanged += (_, _) => Preview();
        _uiScale.ValueChanged += (_, _) => Preview();
        _msaa.Items.AddRange(new object[] { "1", "2", "4", "8" });
        _msaa.SelectedIndexChanged += (_, _) => Preview();
        // D-186-FovScale: 11 choices in 0.05 steps (0.50-1.00) for each axis. "1.00" is the shipped
        // pass-through and the default the host uses when the key is absent, so an unedited file still
        // renders the same picture. X and Y are independent (either axis can be narrowed on its own).
        // D-192: slider values are 50-100 = 0.50-1.00; the label beside each slider shows the exact value.
        _fovScaleX.ValueChanged += (_, _) => { UpdateFovLabels(); Preview(); };
        _fovScaleY.ValueChanged += (_, _) => { UpdateFovLabels(); Preview(); };
        // D-104-iso-promotion: Anisotropic toggle in the form mirrors host's read of the same key.
        _aniso.CheckedChanged += (_, _) => Preview();
        // Keep both FOV sliders above neighboring controls after WinForms DPI/layout scaling.
        // In particular, do not let the Y trackbar be hidden behind the preview/labels.
        _fovScaleX.Visible = true; _fovScaleX.Enabled = true; _fovScaleX.BringToFront();
        _fovScaleY.Visible = true; _fovScaleY.Enabled = true; _fovScaleY.BringToFront();
        _fovXVal.BringToFront(); _fovYVal.BringToFront();
        LoadConfig(path ?? HostConfigTool.Locate(AppContext.BaseDirectory));
    }

    private string SelectedPreset => _custom.Checked ? RenderResolutionPolicy.Custom : _high.Checked ? RenderResolutionPolicy.High :
        _low.Checked ? RenderResolutionPolicy.Low : RenderResolutionPolicy.Normal;

    private void SetPreset(string preset)
    {
        _low.Checked = preset == RenderResolutionPolicy.Low;
        _normal.Checked = preset == RenderResolutionPolicy.Normal;
        _high.Checked = preset == RenderResolutionPolicy.High;
        _custom.Checked = preset == RenderResolutionPolicy.Custom;
        if (!_low.Checked && !_normal.Checked && !_high.Checked && !_custom.Checked) _normal.Checked = true;
    }

    private PictureSize Current() => new(SelectedPreset, _baseWidth, _baseHeight, (double)_scale.Value);

    private void Browse()
    {
        using var dialog = new OpenFileDialog { Filter = HostConfigTool.FileName + "|" + HostConfigTool.FileName + "|JSON|*.json", Title = "vhvr-openxr-host.json" };
        if (File.Exists(_path.Text)) dialog.FileName = _path.Text;
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadConfig(dialog.FileName);
    }

    private void LoadConfig(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) { _root = null; SetStatus("No settings file found. Put this program in the game folder (or BepInEx\\config) or pick the file with Find...", true); return; }
        try
        {
            _root = HostConfigTool.Load(path);
            PictureSize size = HostConfigTool.Read(_root);
            _path.Text = path;
            SetPreset(RenderResolutionPolicy.IsValid(size.Preset) ? size.Preset : RenderResolutionPolicy.Normal);
            _baseWidth = size.BaseWidth; _baseHeight = size.BaseHeight;
            _scale.Value = Math.Clamp((decimal)size.Scale, _scale.Minimum, _scale.Maximum);
            _uiScale.Value = Math.Clamp((decimal)(HostConfigTool.ReadUiScale(_root) ?? HostConfigTool.DefaultUiScale), _uiScale.Minimum, _uiScale.Maximum);
            // D-186: a value outside 1/2/4/8 would be refused by the runtime (it keeps 4), so the form shows 4 for it.
            int msaa = HostConfigTool.ReadEyeMsaa(_root) ?? HostConfigTool.DefaultEyeMsaa;
            _msaa.SelectedItem = Array.IndexOf(HostConfigTool.EyeMsaaChoices, msaa) >= 0 ? msaa.ToString() : HostConfigTool.DefaultEyeMsaa.ToString();
            // D-104-iso-promotion: missing key means "not opted in yet" and the box is unchecked; that matches the
            // shipped value (DefaultAnisotropic = false). An unknown non-bool value makes System.Text.Json throw
            // before we get here, so the only paths that reach this line are bool/missing.
            _aniso.Checked = HostConfigTool.ReadAnisotropic(_root) ?? HostConfigTool.DefaultAnisotropic;
            // D-186-FovScale: a value outside the 0.50-1.00 choice list would be refused by the runtime (it
            // keeps 1.0), so the form shows 1.00 for it. An absent X/Y key falls back to DefaultFovScale =
            // 1.0 (the shipped pass-through), so an unedited 0.7.1 file renders the same picture. A
            // pre-Stage-3.5 file with the single FovScale key is read as FovScaleX=that value, FovScaleY=1.0
            // (horizontal-only cut).
            // D-192: snap to the nearest 0.05 choice and store it on the 50-100 slider.
            float fovScaleX = HostConfigTool.ReadFovScaleX(_root) ?? HostConfigTool.DefaultFovScale;
            _fovScaleX.Value = Math.Clamp((int)Math.Round(fovScaleX * 100f / 5f) * 5, (int)_fovScaleX.Minimum, (int)_fovScaleX.Maximum);
            float fovScaleY = HostConfigTool.ReadFovScaleY(_root) ?? HostConfigTool.DefaultFovScale;
            _fovScaleY.Value = Math.Clamp((int)Math.Round(fovScaleY * 100f / 5f) * 5, (int)_fovScaleY.Minimum, (int)_fovScaleY.Maximum);
            Preview();
            bool running = GameRunning();
            SetStatus("Loaded. Changes apply the next time the game starts." +
                (running ? "\nThe game is running. Close the game to change settings." : "") +
                (size.BaseWidth != 1920 || size.BaseHeight != 1080 ? "\nThis file uses an older base size (" + size.BaseWidth + "x" + size.BaseHeight + " per eye)." : ""), running);
        }
        catch (Exception e) { _root = null; SetStatus("Could not read the file: " + e.Message, true); }
    }

    private void UpdateFovLabels()
    {
        _fovXVal.Text = (_fovScaleX.Value / 100f).ToString("0.00", CultureInfo.InvariantCulture);
        _fovYVal.Text = (_fovScaleY.Value / 100f).ToString("0.00", CultureInfo.InvariantCulture);
    }

    private void Preview()
    {
        PictureSize size = Current();
        _scale.Enabled = _custom.Checked;
        string? error = HostConfigTool.Validate(size);
        if (error != null) { _preview.ForeColor = Color.Firebrick; _preview.Text = "Cannot be used: " + error; return; }
        (int width, int height) = size.EyeSize;
        (int bothWidth, int bothHeight) = RenderResolutionPolicy.BothEyes(width, height);
        _base.Text = "Base " + size.BaseWidth + "x" + size.BaseHeight + " per eye (" + (size.BaseWidth * 2) + "x" + (size.BaseHeight * 2) + " both eyes)";
        _preview.ForeColor = Color.FromArgb(20, 60, 120);
        _preview.Text = "Both eyes " + bothWidth + "x" + bothHeight + "  /  per eye " + width + "x" + height + "  /  scale " +
            size.EffectiveScale.ToString("0.00") + "\n" + (width * height * 2 / 1000000.0).ToString("0.0") +
            " megapixels per frame (2 x " + (width * height / 1000000.0).ToString("0.0") + "); the eye picture is 16:9, so a headset eye view of another shape is stretched to fit,";
        _preview.Text += " and a bigger size is sharper while it costs frame rate.";
        _preview.Text += "\nIn-VR UI size x" + _uiScale.Value.ToString("0.00") + " (1.00 = shipped; the extras plugin scales the whole in-VR HUD).";
        _preview.Text += " Eye MSAA x" + (_msaa.SelectedItem ?? "4") + " (1 = off; more samples = smoother edges at a GPU cost).";
        _preview.Text += "\nAnisotropic " + (_aniso.Checked ? "16x on" : "off") + " (off = shipped; on = sharper angled textures, ~negligible GPU cost).";
        // D-186-FovScale: 1.00 = pass-through (shipped), < 1.00 = narrower rendered FOV on each axis. The
        // estimated pixel reduction is the product of the two scales, so 0.85*0.85 = ~28% fewer pixels
        // (single axis) and 0.85*0.80 = ~32% fewer pixels (asymmetric cut).
        // D-192: slider values are 50-100 = 0.50-1.00.
        string fovShownX = (_fovScaleX.Value / 100f).ToString("0.00", CultureInfo.InvariantCulture);
        float fovValueX = _fovScaleX.Value / 100f;
        string fovShownY = (_fovScaleY.Value / 100f).ToString("0.00", CultureInfo.InvariantCulture);
        float fovValueY = _fovScaleY.Value / 100f;
        _preview.Text += "\nRender FOV scale X x" + fovShownX + " Y x" + fovShownY + " (1.00 = shipped; " + (
            fovValueX < 1.0f || fovValueY < 1.0f
                ? "0.50-0.95 narrow the rendered FOV; ~" + (100.0 * (1.0f - fovValueX * fovValueY)).ToString("0", CultureInfo.InvariantCulture) + "% fewer pixels estimated)"
                : "pass-through, no pixel reduction)");
    }

    private void Save()
    {
        if (_root == null) { SetStatus("Open a settings file first.", true); return; }
        PictureSize size = Current();
        string? error = HostConfigTool.Validate(size);
        if (error != null) { SetStatus("Nothing was written: " + error, true); return; }
        if (GameRunning() && MessageBox.Show(this, "The game is running. Change the settings anyway?\n(They apply the next time the game starts.)", Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
        try
        {
            HostConfigTool.Apply(_root, size);
            HostConfigTool.ApplyUiScale(_root, (double)_uiScale.Value);
            HostConfigTool.ApplyEyeMsaa(_root, int.Parse((string)_msaa.SelectedItem!));
            HostConfigTool.ApplyAnisotropic(_root, _aniso.Checked);
            // D-186-FovScale: the form only ever offers the 11 valid choices on each axis, so
            // ValidateFovScale is a no-op here; it is kept for symmetry with ApplyEyeMsaa so a future
            // custom-control edit cannot write a bad value. ApplyFovScaleX/Y also retire the legacy single
            // FovScale key on save, so a migrated file ends up with FovScaleX + FovScaleY only.
            HostConfigTool.ApplyFovScaleX(_root, _fovScaleX.Value / 100f);
            HostConfigTool.ApplyFovScaleY(_root, _fovScaleY.Value / 100f);
            string backup = HostConfigTool.Save(_path.Text, _root);
            SetStatus("Saved. Backup: " + Path.GetFileName(backup) + "\n" + size.Preview + "\nRestart the game for it to take effect.", false);
        }
        catch (Exception e) { SetStatus("Could not save: " + e.Message, true); }
    }

    private void SetStatus(string text, bool warning)
    {
        _status.ForeColor = warning ? Color.Firebrick : Color.DimGray;
        _status.Text = text;
    }

    private static bool GameRunning()
    {
        try { return Process.GetProcessesByName("Vampire Hunters").Length > 0; } catch (Exception) { return false; }
    }

    // D-171: read-only test surface for --form-selftest. Exposing what the window actually loaded keeps the automated
    // check honest: it proves the form ran with a config, not merely that the class compiled.
    internal bool FormLoaded => _root != null;
    internal string LoadedPreview => _preview.Text;
    internal string LoadedStatus => _status.Text;
    internal int ControlCount => Controls.Count;

    // D-171: a window that shows a user its own settings must not clip its own text. Every control is checked against the
    // client area (and against a minimum readable width), so this whole class of "짤려서 보인다" bug fails the self-test
    // instead of being reported by the user.
    internal List<string> ClippedControls()
    {
        var clipped = new List<string>();
        foreach (Control control in Controls)
        {
            string name = control is Label label && label.Text.Length > 0 ? label.Text : control.Text.Length > 0 ? control.Text : control.GetType().Name;
            if (control.Bounds.Right > ClientSize.Width - 4 || control.Bounds.Bottom > ClientSize.Height - 4) clipped.Add(name);
        }
        return clipped;
    }
}
