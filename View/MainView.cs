using Godot;
using System.ComponentModel;
using System.Text;
using word_input.Model;
using word_input.Services;
using word_input.ViewModel;

namespace word_input.View;

/// <summary>View 层：把 ViewModel 的属性绑定到控件，把按键翻译成 ViewModel 命令。</summary>
public partial class MainView : Control
{
	private const string WordsPath = "res://words.json";
	private const string ColorCorrect = "#4caf50";
	private const string ColorWrong = "#e53935";
	private const string ColorReveal = "#ff9800";
	private const string ColorPending = "#6b7484";
	private const string ColorCaret = "#aab3c4";

	private MainViewModel _vm;
	private Label _progressLabel;
	private LinkButton _prevButton;
	private LinkButton _nextButton;
	private RichTextLabel _wordLabel;
	private VBoxContainer _transBox;
	private Label _phoneLabel;
	private Button _speakButton;
	private Label _resultLabel;
	private Label _statsLabel;
	private Button _restartButton;

	public override void _Ready()
	{
		_progressLabel = GetNode<Label>("Margin/VBox/ProgressLabel");
		_prevButton = GetNode<LinkButton>("Margin/VBox/NeighborBox/PrevButton");
		_nextButton = GetNode<LinkButton>("Margin/VBox/NeighborBox/NextButton");
		_wordLabel = GetNode<RichTextLabel>("Margin/VBox/WordLabel");
		_transBox = GetNode<VBoxContainer>("Margin/VBox/TransBox");
		_phoneLabel = GetNode<Label>("Margin/VBox/PhoneLabel");
		_speakButton = GetNode<Button>("Margin/VBox/SpeakButton");
		_resultLabel = GetNode<Label>("Margin/VBox/ResultLabel");
		_statsLabel = GetNode<Label>("Margin/VBox/StatsLabel");
		_restartButton = GetNode<Button>("Margin/VBox/RestartButton");

		var pronunciation = new YoudaoPronunciationService(this);
		_vm = new MainViewModel(new WordRepository(WordsPath), pronunciation);
		_vm.PropertyChanged += OnViewModelPropertyChanged;
		_restartButton.Pressed += _vm.Restart;
		_speakButton.Pressed += _vm.SpeakCurrent;
		_prevButton.Pressed += _vm.JumpPrev;
		_nextButton.Pressed += _vm.JumpNext;
		_vm.Start();
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent || !keyEvent.Pressed)
			return;
		switch (keyEvent.Keycode)
		{
			case Key.Backspace:
				_vm.Backspace(); // 退格允许按住连删
				break;
			case Key.Enter:
			case Key.KpEnter:
				if (!keyEvent.Echo)
					_vm.Submit();
				break;
			case Key.Space:
				if (!keyEvent.Echo)
					_vm.Advance();
				break;
			default:
				if (keyEvent.Echo)
					break;
				char ch = '\0';
				if (keyEvent.Keycode >= Key.A && keyEvent.Keycode <= Key.Z)
					ch = char.ToLowerInvariant((char)keyEvent.Keycode);
				else if (keyEvent.Unicode > 32)
					ch = char.ToLowerInvariant((char)keyEvent.Unicode);
				if (ch != '\0')
					_vm.TypeChar(ch);
				break;
		}
	}

	// —— 属性绑定 ——

	private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(MainViewModel.ProgressText):
				_progressLabel.Text = _vm.ProgressText;
				break;
			case nameof(MainViewModel.Word):
			case nameof(MainViewModel.TypedText):
			case nameof(MainViewModel.Judged):
			case nameof(MainViewModel.ShowPlain):
				UpdateWordDisplay();
				break;
			case nameof(MainViewModel.Result):
			case nameof(MainViewModel.CorrectAnswer):
				UpdateResult();
				break;
			case nameof(MainViewModel.PhoneticsText):
				_phoneLabel.Text = _vm.PhoneticsText;
				break;
			case nameof(MainViewModel.Translations):
				RebuildTranslations();
				break;
			case nameof(MainViewModel.StatsText):
				_statsLabel.Text = _vm.StatsText;
				break;
			case nameof(MainViewModel.PrevWord):
			case nameof(MainViewModel.HasPrev):
				_prevButton.Visible = _vm.HasPrev;
				_prevButton.Text = $"← {_vm.PrevWord}";
				break;
			case nameof(MainViewModel.NextWord):
			case nameof(MainViewModel.HasNext):
				_nextButton.Visible = _vm.HasNext;
				_nextButton.Text = $"{_vm.NextWord} →";
				break;
			case nameof(MainViewModel.RoundFinished):
				UpdateWordDisplay();
				_restartButton.Visible = _vm.RoundFinished;
				if (_vm.RoundFinished)
					_restartButton.GrabFocus();
				break;
		}
	}

	// —— 渲染 ——

	private void UpdateWordDisplay()
	{
		if (_vm.RoundFinished || _vm.ShowPlain)
		{
			_wordLabel.Text = $"[center]{_vm.Word}[/center]";
			return;
		}
		string target = _vm.Word;
		var sb = new StringBuilder("[center]");
		if (_vm.Judged)
		{
			// 判定后整词按对错上色
			string revealColor = _vm.Result == ResultKind.Correct ? ColorCorrect : ColorReveal;
			foreach (char c in target)
				sb.Append($"[color={revealColor}]{c}[/color]");
		}
		else
		{
			string typed = _vm.TypedText;
			for (int i = 0; i < target.Length; i++)
			{
				if (i < typed.Length)
				{
					string col = typed[i] == target[i] ? ColorCorrect : ColorWrong;
					sb.Append($"[color={col}]{target[i]}[/color]");
				}
				else if (i == typed.Length)
					sb.Append($"[u][color={ColorCaret}]{target[i]}[/color][/u]");
				else
					sb.Append($"[color={ColorPending}]{target[i]}[/color]");
			}
		}
		sb.Append("[/center]");
		_wordLabel.Text = sb.ToString();
	}

	private void UpdateResult()
	{
		switch (_vm.Result)
		{
			case ResultKind.Correct:
				_resultLabel.Text = "✔ 正确";
				_resultLabel.AddThemeColorOverride("font_color", new Color(ColorCorrect));
				break;
			case ResultKind.Wrong:
				_resultLabel.Text = $"✘ 正确答案：{_vm.CorrectAnswer}";
				_resultLabel.AddThemeColorOverride("font_color", new Color(ColorWrong));
				break;
			default:
				_resultLabel.Text = "";
				break;
		}
	}

	private void RebuildTranslations()
	{
		foreach (var child in _transBox.GetChildren())
		{
			_transBox.RemoveChild(child);
			child.Free();
		}
		foreach (var line in _vm.Translations)
		{
			var label = new Label
			{
				Text = line,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			};
			label.AddThemeFontSizeOverride("font_size", 24);
			label.AddThemeColorOverride("font_color", new Color(0.85f, 0.88f, 0.93f));
			_transBox.AddChild(label);
		}
	}
}
