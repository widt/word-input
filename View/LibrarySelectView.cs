using Godot;
using System.Collections.Generic;
using word_input.Model;

namespace word_input.View;

/// <summary>主场景：选择词库 JSON，设置排序方式与练习范围，然后进入练习场景。</summary>
public partial class LibrarySelectView : Control
{
	private const int PageSize = 20; // 挑选面板每页展示的单词数

	private readonly List<WordEntry> _words = [];
	private string _path = "";
	private bool _randomOrder = true;
	private List<int> _selectedIndices; // null 表示未手动挑选，按范围/全部练习
	private int _pageIndex;

	private Label _errorLabel;
	private Control _optionsBox;
	private Label _loadedLabel;
	private Button _orderButton;
	private SpinBox _startSpin;
	private SpinBox _endSpin;
	private Label _selectionLabel;
	private Control _wordPanel;
	private GridContainer _grid;
	private Label _pageLabel;
	private Button _prevPageButton;
	private Button _nextPageButton;
	private FileDialog _fileDialog;

	/// <summary>获取控件引用并挂接全部按钮与对话框事件，把文件对话框初始目录定位到工程目录。</summary>
	public override void _Ready()
	{
		_errorLabel = GetNode<Label>("Center/VBox/ErrorLabel");
		_optionsBox = GetNode<Control>("Center/VBox/OptionsBox");
		_loadedLabel = GetNode<Label>("Center/VBox/OptionsBox/LoadedLabel");
		_orderButton = GetNode<Button>("Center/VBox/OptionsBox/OrderButton");
		_startSpin = GetNode<SpinBox>("Center/VBox/OptionsBox/RangeBox/StartSpin");
		_endSpin = GetNode<SpinBox>("Center/VBox/OptionsBox/RangeBox/EndSpin");
		_selectionLabel = GetNode<Label>("Center/VBox/OptionsBox/SelectionLabel");
		_wordPanel = GetNode<Control>("WordPanel");
		_grid = GetNode<GridContainer>("WordPanel/Margin/VBox/Grid");
		_pageLabel = GetNode<Label>("WordPanel/Margin/VBox/PageBox/PageLabel");
		_prevPageButton = GetNode<Button>("WordPanel/Margin/VBox/PageBox/PrevPageButton");
		_nextPageButton = GetNode<Button>("WordPanel/Margin/VBox/PageBox/NextPageButton");
		_fileDialog = GetNode<FileDialog>("FileDialog");

		GetNode<Button>("Center/VBox/FileBox/DefaultButton").Pressed += UseDefaultLibrary;
		GetNode<Button>("Center/VBox/FileBox/PickButton").Pressed += () => _fileDialog.PopupCentered();
		_fileDialog.FileSelected += LoadLibrary;
		_orderButton.Pressed += ToggleOrder;
		_startSpin.ValueChanged += _ => OnRangeChanged();
		_endSpin.ValueChanged += _ => OnRangeChanged();
		GetNode<Button>("Center/VBox/OptionsBox/PickWordsButton").Pressed += ShowWordPanel;
		GetNode<Button>("Center/VBox/OptionsBox/StartButton").Pressed += StartPractice;
		GetNode<Button>("WordPanel/Margin/VBox/HeadBox/SelectAllButton").Pressed += SelectAll;
		GetNode<Button>("WordPanel/Margin/VBox/HeadBox/CloseButton").Pressed += () => _wordPanel.Visible = false;
		_prevPageButton.Pressed += () => ShowWordPage(_pageIndex - 1);
		_nextPageButton.Pressed += () => ShowWordPage(_pageIndex + 1);

		string dir = ProjectSettings.GlobalizePath("res://");
		if (DirAccess.DirExistsAbsolute(dir))
			_fileDialog.CurrentDir = dir;
	}

	/// <summary>使用工程自带的 words.json 加载词库。</summary>
	private void UseDefaultLibrary() => LoadLibrary("res://words.json");

	/// <summary>加载所选 JSON；有效则显示选项区并复位选择状态。</summary>
	private void LoadLibrary(string path)
	{
		var words = WordRepository.Load(path);
		if (words.Count == 0)
		{
			_errorLabel.Text = "无法读取该文件，请确认是有效的词库 JSON";
			return;
		}
		_path = path;
		_words.Clear();
		_words.AddRange(words);
		_selectedIndices = null;
		_errorLabel.Text = "";
		_optionsBox.Visible = true;
		_loadedLabel.Text = $"已加载 {System.IO.Path.GetFileName(path)}，共 {words.Count} 个词";
		_startSpin.MaxValue = words.Count;
		_startSpin.Value = 1;
		_endSpin.MaxValue = words.Count;
		_endSpin.Value = words.Count;
		UpdateSelectionLabel();
	}

	/// <summary>切换顺序/随机排序并更新按钮文案。</summary>
	private void ToggleOrder()
	{
		_randomOrder = !_randomOrder;
		_orderButton.Text = _randomOrder ? "排序：随机" : "排序：顺序";
	}

	/// <summary>调整范围时放弃手动挑选结果并刷新提示。</summary>
	private void OnRangeChanged()
	{
		_selectedIndices = null;
		UpdateSelectionLabel();
	}

	/// <summary>打开逐词挑选面板并回到第一页。</summary>
	private void ShowWordPanel()
	{
		_wordPanel.Visible = true;
		ShowWordPage(0);
	}

	/// <summary>重建指定页的单词复选框并更新翻页按钮状态。</summary>
	private void ShowWordPage(int page)
	{
		int pageCount = Mathf.Max(1, (_words.Count + PageSize - 1) / PageSize);
		_pageIndex = Mathf.Clamp(page, 0, pageCount - 1);
		foreach (var child in _grid.GetChildren())
		{
			_grid.RemoveChild(child);
			child.Free();
		}
		int start = _pageIndex * PageSize;
		for (int i = start; i < Mathf.Min(start + PageSize, _words.Count); i++)
		{
			var box = new CheckBox { ButtonPressed = IsManuallySelected(i) };
			box.Text = $"{i + 1}. {_words[i].Name}";
			box.AddThemeFontSizeOverride("font_size", 20);
			int index = i;
			box.Toggled += on => OnWordToggled(index, on);
			_grid.AddChild(box);
		}
		_pageLabel.Text = $"第 {_pageIndex + 1} / {pageCount} 页";
		_prevPageButton.Disabled = _pageIndex == 0;
		_nextPageButton.Disabled = _pageIndex >= pageCount - 1;
	}

	/// <summary>该词是否在练习范围内（未手动挑选时视为全选）。</summary>
	private bool IsManuallySelected(int index) =>
		_selectedIndices == null || _selectedIndices.Contains(index);

	/// <summary>勾选/取消单个词；全部勾选时还原为未手动挑选状态。</summary>
	private void OnWordToggled(int index, bool on)
	{
		if (_selectedIndices == null)
		{
			_selectedIndices = [];
			for (int i = 0; i < _words.Count; i++)
				_selectedIndices.Add(i);
		}
		if (on && !_selectedIndices.Contains(index))
			_selectedIndices.Add(index);
		else if (!on)
			_selectedIndices.Remove(index);
		if (_selectedIndices.Count == _words.Count)
			_selectedIndices = null;
		UpdateSelectionLabel();
	}

	/// <summary>清空手动挑选，恢复练习全部词。</summary>
	private void SelectAll()
	{
		_selectedIndices = null;
		ShowWordPage(_pageIndex);
		UpdateSelectionLabel();
	}

	/// <summary>按当前模式更新"将练习"提示文案：手动挑选优先，其次范围，否则全部。</summary>
	private void UpdateSelectionLabel()
	{
		if (_words.Count == 0)
		{
			_selectionLabel.Text = "";
			return;
		}
		if (_selectedIndices != null)
			_selectionLabel.Text = $"将练习：手动选择 {_selectedIndices.Count} 个词";
		else if (_startSpin.Value > 1 || _endSpin.Value < _words.Count)
			_selectionLabel.Text = $"将练习：第 {(int)_startSpin.Value} – {(int)_endSpin.Value} 个（共 {(int)(_endSpin.Value - _startSpin.Value + 1)} 个词）";
		else
			_selectionLabel.Text = $"将练习：全部 {_words.Count} 个词";
	}

	/// <summary>把词库路径、排序方式与词条索引写入 PracticeSetup，切换到练习场景。</summary>
	private void StartPractice()
	{
		PracticeSetup.WordsPath = _path;
		PracticeSetup.RandomOrder = _randomOrder;
		if (_selectedIndices != null)
		{
			_selectedIndices.Sort();
			PracticeSetup.Indices = new List<int>(_selectedIndices);
		}
		else if (_startSpin.Value > 1 || _endSpin.Value < _words.Count)
		{
			var indices = new List<int>();
			for (int i = (int)_startSpin.Value - 1; i < (int)_endSpin.Value; i++)
				indices.Add(i);
			PracticeSetup.Indices = indices;
		}
		else
		{
			PracticeSetup.Indices = null;
		}
		GetTree().ChangeSceneToFile("res://practice.tscn");
	}
}
