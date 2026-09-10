using Godot;
using System.Text;
using word_input.Services;
using word_input.ViewModel;

namespace word_input.View;

/// <summary>View 层：把按键翻译成 ViewModel 命令，ViewModel 变化后整体刷新控件。</summary>
public partial class MainView : Control
{
	/// <summary>词库路径，由词库选择场景在切换场景前设置；为空时使用默认词库。</summary>
	public static string SelectedWordsPath;

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
	private LinkButton _backButton;
	private string _lastWord; // 译文区只在换词时重建

	/// <summary>获取控件引用，创建发音服务与 ViewModel，挂接按钮命令后启动第一轮。</summary>
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
		_backButton = GetNode<LinkButton>("Margin/VBox/BackButton");

		_vm = new MainViewModel(new Pronunciation(this), SelectedWordsPath ?? "res://words.json");
		_vm.Changed += Refresh;
		_restartButton.Pressed += _vm.Restart;
		_speakButton.Pressed += _vm.SpeakCurrent;
		_prevButton.Pressed += _vm.JumpPrev;
		_nextButton.Pressed += _vm.JumpNext;
		_backButton.Pressed += OnBack;
		_vm.Start();
	}

	/// <summary>返回词库选择场景。</summary>
	private void OnBack() => GetTree().ChangeSceneToFile("res://main.tscn");

	/// <summary>把按键翻译成 ViewModel 命令：退格删除、回车判定、空格前进/跳过、字母输入。</summary>
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

	/// <summary>读取 ViewModel 全部状态刷新所有控件；译文区仅在换词时重建。</summary>
	private void Refresh()
	{
		_progressLabel.Text = _vm.ProgressText;
		UpdateWordDisplay();
		UpdateResult();
		_phoneLabel.Text = _vm.PhoneticsText;
		_statsLabel.Text = _vm.StatsText;
		_prevButton.Visible = _vm.HasPrev;
		_prevButton.Text = $"← {_vm.PrevWord}";
		_nextButton.Visible = _vm.HasNext;
		_nextButton.Text = $"{_vm.NextWord} →";
		_restartButton.Visible = _vm.RoundFinished;
		if (_vm.RoundFinished)
			_restartButton.GrabFocus();
		if (_lastWord != _vm.Word)
		{
			_lastWord = _vm.Word;
			RebuildTranslations();
		}
	}

	/// <summary>按输入进度渲染单词：已输入按对错配色、待输入处显示光标下划线、其余灰色；判定后整词上色。</summary>
	private void UpdateWordDisplay()
	{
		if (_vm.RoundFinished || _vm.ShowPlain)
		{
			_wordLabel.Text = $"[center]{_vm.Word}[/center]";
			return;
		}
		var sb = new StringBuilder("[center]");
		if (_vm.Judged)
		{
			// 判定后整词按对错上色
			string revealColor = _vm.Result == ResultKind.Correct ? ColorCorrect : ColorReveal;
			foreach (char c in _vm.Word)
				sb.Append($"[color={revealColor}]{c}[/color]");
		}
		else
		{
			string typed = _vm.TypedText;
			for (int i = 0; i < _vm.Word.Length; i++)
			{
				if (i < typed.Length)
				{
					string col = typed[i] == _vm.Word[i] ? ColorCorrect : ColorWrong;
					sb.Append($"[color={col}]{_vm.Word[i]}[/color]");
				}
				else if (i == typed.Length)
				{
					sb.Append($"[u][color={ColorCaret}]{_vm.Word[i]}[/color][/u]");
				}
				else
				{
					sb.Append($"[color={ColorPending}]{_vm.Word[i]}[/color]");
				}
			}
		}
		sb.Append("[/center]");
		_wordLabel.Text = sb.ToString();
	}

	/// <summary>按判定结果显示文案与颜色：正确、错误（附正确答案）或清空。</summary>
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

	/// <summary>清空并按当前词的译文列表重建译文区标签。</summary>
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
