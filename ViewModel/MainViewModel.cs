using System.Collections.Generic;
using System.Linq;
using word_input.Model;
using word_input.Services;

namespace word_input.ViewModel;

public enum ResultKind
{
	None,
	Correct,
	Wrong,
}

/// <summary>打字练习的全部状态与规则；不依赖任何 UI 控件。</summary>
public sealed class MainViewModel : ViewModelBase
{
	private readonly IWordRepository _repository;
	private readonly IPronunciationService _pronunciation;
	private readonly List<WordEntry> _words = [];
	private readonly List<int> _order = [];

	private int _currentPos;
	private int _correctCount;
	private int _wrongCount;
	private string _typed = "";
	private bool _judged;
	private ResultKind _result = ResultKind.None;

	public MainViewModel(IWordRepository repository, IPronunciationService pronunciation)
	{
		_repository = repository;
		_pronunciation = pronunciation;
	}

	public bool HasWords => _words.Count > 0;
	public bool RoundFinished { get; private set; }
	public bool ShowPlain { get; private set; }
	public string ProgressText { get; private set; } = "";
	public string Word { get; private set; } = "";
	public string TypedText => _typed;
	public bool Judged => _judged;
	public ResultKind Result => _result;
	public string CorrectAnswer { get; private set; } = "";
	public string PhoneticsText { get; private set; } = "";
	public IReadOnlyList<string> Translations { get; private set; } = [];
	public string StatsText { get; private set; } = "";
	public string PrevWord { get; private set; } = "";
	public bool HasPrev { get; private set; }
	public string NextWord { get; private set; } = "";
	public bool HasNext { get; private set; }

	/// <summary>加载词条并开始第一轮。</summary>
	public void Start()
	{
		foreach (var word in _repository.Load())
		{
			if (word.Name.Length > 0)
				_words.Add(word);
		}
		Restart();
	}

	/// <summary>重新洗牌并开始新一轮，清零统计。</summary>
	public void Restart()
	{
		_order.Clear();
		for (int i = 0; i < _words.Count; i++)
			_order.Add(i);
		Shuffle(_order);
		_currentPos = 0;
		_correctCount = 0;
		_wrongCount = 0;
		RoundFinished = false;
		UpdateStats();
		if (!HasWords)
		{
			ProgressText = "";
			Word = "读取 words.json 失败";
			ShowPlain = true;
			PhoneticsText = "";
			Translations = [];
			_result = ResultKind.None;
			HasPrev = false;
			HasNext = false;
			Raise(nameof(ProgressText), nameof(Word), nameof(ShowPlain), nameof(PhoneticsText),
				nameof(Translations), nameof(Result), nameof(HasPrev), nameof(HasNext), nameof(RoundFinished));
			return;
		}
		ShowCurrentWord();
	}

	// —— 输入命令：由 View 把按键翻译成这些意图 ——

	public void TypeChar(char c)
	{
		if (!CanType() || _typed.Length >= Word.Length)
			return;
		_typed += c;
		OnPropertyChanged(nameof(TypedText));
		if (_typed == Target())
			Judge();
	}

	public void Backspace()
	{
		if (!CanType() || _typed.Length == 0)
			return;
		_typed = _typed[..^1];
		OnPropertyChanged(nameof(TypedText));
	}

	public void Submit()
	{
		if (!CanType() || _typed.Length == 0)
			return;
		Judge();
	}

	/// <summary>空格：已判定则进入下一个词；输入中且已打字则视为放弃本题，计错误并跳到下一个词。</summary>
	public void Advance()
	{
		if (!HasWords || RoundFinished)
			return;
		if (_judged)
		{
			MoveNext();
			return;
		}
		if (_typed.Length > 0)
		{
			_wrongCount++;
			UpdateStats();
			MoveNext();
		}
	}

	public void JumpPrev() => Jump(-1);

	public void JumpNext() => Jump(1);

	public void SpeakCurrent()
	{
		if (!HasWords || RoundFinished)
			return;
		_pronunciation.Speak(Target());
	}

	// —— 内部逻辑 ——

	private bool CanType() => HasWords && !RoundFinished && !_judged;

	private WordEntry CurrentWord() => _words[_order[_currentPos]];

	private string WordNameAt(int pos) => _words[_order[pos]].Name.ToLowerInvariant();

	private string Target() => WordNameAt(_currentPos);

	private void Jump(int delta)
	{
		int pos = _currentPos + delta;
		if (pos < 0 || pos >= _words.Count)
			return;
		_currentPos = pos;
		ShowCurrentWord();
	}

	private void ShowCurrentWord()
	{
		_judged = false;
		_typed = "";
		_result = ResultKind.None;
		ShowPlain = false;
		var word = CurrentWord();
		ProgressText = $"第 {_currentPos + 1} / {_words.Count} 个";
		Word = word.Name.ToLowerInvariant();
		CorrectAnswer = word.Name;
		PhoneticsText = $"英 {word.UkPhone}    美 {word.UsPhone}";
		Translations = word.Trans.ToList();
		UpdateNeighbors();
		Raise(nameof(ProgressText), nameof(Word), nameof(TypedText), nameof(Judged), nameof(Result),
			nameof(ShowPlain), nameof(CorrectAnswer), nameof(PhoneticsText), nameof(Translations),
			nameof(PrevWord), nameof(HasPrev), nameof(NextWord), nameof(HasNext));
		_pronunciation.Speak(Target());
		PrefetchNextAudio();
	}

	private void Judge()
	{
		_judged = true;
		bool ok = _typed == Target();
		if (ok)
			_correctCount++;
		else
			_wrongCount++;
		_result = ok ? ResultKind.Correct : ResultKind.Wrong;
		UpdateStats();
		Raise(nameof(Judged), nameof(Result), nameof(StatsText));
	}

	private void MoveNext()
	{
		_currentPos++;
		if (_currentPos >= _words.Count)
			Finish();
		else
			ShowCurrentWord();
	}

	private void Finish()
	{
		RoundFinished = true;
		_judged = false;
		_typed = "";
		_result = ResultKind.None;
		ShowPlain = true;
		ProgressText = "已完成";
		Word = "本轮结束！";
		CorrectAnswer = "";
		PhoneticsText = "";
		Translations = [];
		HasPrev = false;
		HasNext = false;
		Raise(nameof(RoundFinished), nameof(ProgressText), nameof(Word), nameof(TypedText), nameof(Judged),
			nameof(Result), nameof(ShowPlain), nameof(CorrectAnswer), nameof(PhoneticsText),
			nameof(Translations), nameof(PrevWord), nameof(HasPrev), nameof(NextWord), nameof(HasNext));
		_pronunciation.Stop();
	}

	/// <summary>预取后面两个词的发音，翻到时无需等待即可播放。</summary>
	private void PrefetchNextAudio()
	{
		for (int i = 1; i <= 2 && _currentPos + i < _words.Count; i++)
			_pronunciation.Prefetch(WordNameAt(_currentPos + i));
	}

	private void UpdateNeighbors()
	{
		HasPrev = _currentPos > 0;
		PrevWord = HasPrev ? _words[_order[_currentPos - 1]].Name : "";
		HasNext = _currentPos < _words.Count - 1;
		NextWord = HasNext ? _words[_order[_currentPos + 1]].Name : "";
	}

	private void UpdateStats() =>
		StatsText = $"正确 {_correctCount}   错误 {_wrongCount}";

	private static void Shuffle<T>(List<T> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = System.Random.Shared.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
