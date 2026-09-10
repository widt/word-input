using System;
using System.Collections.Generic;
using word_input.Model;
using word_input.Services;

namespace word_input.ViewModel;

public enum ResultKind
{
	None,
	Correct,
	Wrong,
}

/// <summary>打字练习的全部状态与规则，不依赖任何 UI 控件；状态变化通过 Changed 通知 View 整体刷新。</summary>
public sealed class MainViewModel
{
	/// <summary>任意状态变化后触发，View 收到后读取全部属性刷新界面。</summary>
	public event Action Changed;

	private readonly Pronunciation _pronunciation;
	private readonly string _wordsPath;
	private readonly List<WordEntry> _words = [];
	private readonly List<int> _order = [];

	private int _currentPos;
	private int _correctCount;
	private int _wrongCount;
	private string _typed = "";

	public bool HasWords => _words.Count > 0;
	public bool RoundFinished { get; private set; }
	public bool ShowPlain { get; private set; }
	public string ProgressText { get; private set; } = "";
	public string Word { get; private set; } = ""; // 当前词（小写）；轮次结束后为提示语
	public string TypedText => _typed;
	public bool Judged { get; private set; }
	public ResultKind Result { get; private set; } = ResultKind.None;
	public string CorrectAnswer { get; private set; } = "";
	public string PhoneticsText { get; private set; } = "";
	public IReadOnlyList<string> Translations { get; private set; } = [];
	public string StatsText { get; private set; } = "";
	public bool HasPrev { get; private set; }
	public string PrevWord { get; private set; } = "";
	public bool HasNext { get; private set; }
	public string NextWord { get; private set; } = "";

	/// <summary>创建 ViewModel；wordsPath 为词库 JSON 的路径。</summary>
	public MainViewModel(Pronunciation pronunciation, string wordsPath)
	{
		_pronunciation = pronunciation;
		_wordsPath = wordsPath;
	}

	/// <summary>加载词条并开始第一轮。</summary>
	public void Start()
	{
		foreach (var word in WordRepository.Load(_wordsPath))
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
		_currentPos = _correctCount = _wrongCount = 0;
		RoundFinished = false;
		UpdateStats();
		if (!HasWords)
		{
			ProgressText = "";
			Word = "读取 words.json 失败";
			ShowPlain = true;
			Judged = false;
			Result = ResultKind.None;
			PhoneticsText = "";
			Translations = [];
			HasPrev = HasNext = false;
			Notify();
			return;
		}
		ShowWord();
	}

	// —— 以下方法由 View 把按键/点击翻译后调用 ——

	/// <summary>输入一个字母；打满且全对时自动判定。</summary>
	public void TypeChar(char c)
	{
		if (!CanType() || _typed.Length >= Word.Length)
			return;
		_typed += c;
		if (_typed == Word)
			Judge();
		else
			Notify();
	}

	/// <summary>删除最后一个已输入的字母。</summary>
	public void Backspace()
	{
		if (!CanType() || _typed.Length == 0)
			return;
		_typed = _typed[..^1];
		Notify();
	}

	/// <summary>回车：判定当前输入并显示结果。</summary>
	public void Submit()
	{
		if (!CanType() || _typed.Length == 0)
			return;
		Judge();
	}

	/// <summary>空格：已判定则进入下一个词；输入中已打字则视为放弃本题，计错误并跳到下一个词。</summary>
	public void Advance()
	{
		if (!HasWords || RoundFinished)
			return;
		if (Judged)
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

	/// <summary>跳到顺序中的上一个词。</summary>
	public void JumpPrev() => Jump(-1);

	/// <summary>跳到顺序中的下一个词。</summary>
	public void JumpNext() => Jump(1);

	/// <summary>重新朗读当前词。</summary>
	public void SpeakCurrent()
	{
		if (HasWords && !RoundFinished)
			_pronunciation.Speak(Word);
	}

	// —— 内部逻辑 ——

	/// <summary>当前是否可输入：有词、本轮未结束、尚未判定。</summary>
	private bool CanType() => HasWords && !RoundFinished && !Judged;

	/// <summary>按相对位移跳转，越界时忽略。</summary>
	private void Jump(int delta)
	{
		int pos = _currentPos + delta;
		if (pos < 0 || pos >= _words.Count)
			return;
		_currentPos = pos;
		ShowWord();
	}

	/// <summary>展示当前位置的词：重置输入与结果，更新音标、译文、邻居词，并朗读和预取发音。</summary>
	private void ShowWord()
	{
		Judged = false;
		_typed = "";
		Result = ResultKind.None;
		ShowPlain = false;
		var word = _words[_order[_currentPos]];
		ProgressText = $"第 {_currentPos + 1} / {_words.Count} 个";
		Word = word.Name.ToLowerInvariant();
		CorrectAnswer = word.Name;
		PhoneticsText = $"英 {word.UkPhone}    美 {word.UsPhone}";
		Translations = word.Trans;
		HasPrev = _currentPos > 0;
		PrevWord = HasPrev ? _words[_order[_currentPos - 1]].Name : "";
		HasNext = _currentPos < _words.Count - 1;
		NextWord = HasNext ? _words[_order[_currentPos + 1]].Name : "";
		Notify();
		_pronunciation.Speak(Word);
		for (int i = 1; i <= 2 && _currentPos + i < _words.Count; i++)
			_pronunciation.Prefetch(_words[_order[_currentPos + i]].Name.ToLowerInvariant());
	}

	/// <summary>判定当前输入，按对错更新统计与结果状态。</summary>
	private void Judge()
	{
		Judged = true;
		bool ok = _typed == Word;
		if (ok)
			_correctCount++;
		else
			_wrongCount++;
		Result = ok ? ResultKind.Correct : ResultKind.Wrong;
		UpdateStats();
		Notify();
	}

	/// <summary>前进到下一个词；已是最后一个词则结束本轮。</summary>
	private void MoveNext()
	{
		if (++_currentPos >= _words.Count)
			Finish();
		else
			ShowWord();
	}

	/// <summary>结束本轮：清空当前词信息，显示完成提示，停止朗读。</summary>
	private void Finish()
	{
		RoundFinished = true;
		Judged = false;
		_typed = "";
		Result = ResultKind.None;
		ShowPlain = true;
		ProgressText = "已完成";
		Word = "本轮结束！";
		CorrectAnswer = "";
		PhoneticsText = "";
		Translations = [];
		HasPrev = HasNext = false;
		Notify();
		_pronunciation.Stop();
	}

	/// <summary>按对错计数刷新统计文案。</summary>
	private void UpdateStats() =>
		StatsText = $"正确 {_correctCount}   错误 {_wrongCount}";

	/// <summary>通知 View 整体刷新。</summary>
	private void Notify() => Changed?.Invoke();

	/// <summary>Fisher–Yates 洗牌，打乱词条顺序。</summary>
	private static void Shuffle<T>(List<T> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = Random.Shared.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
