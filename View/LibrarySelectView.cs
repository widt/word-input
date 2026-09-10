using Godot;
using word_input.Model;

namespace word_input.View;

/// <summary>主场景：选择词库 JSON，校验有效后进入练习场景。</summary>
public partial class LibrarySelectView : Control
{
	/// <summary>选中的词库路径，切换场景前设置，练习场景读取一次。</summary>
	public static string SelectedWordsPath;

	private FileDialog _fileDialog;
	private Label _errorLabel;

	/// <summary>获取控件引用，挂接按钮与文件对话框事件，并把对话框初始目录定位到工程目录。</summary>
	public override void _Ready()
	{
		_errorLabel = GetNode<Label>("Center/VBox/ErrorLabel");
		_fileDialog = GetNode<FileDialog>("FileDialog");
		GetNode<Button>("Center/VBox/DefaultButton").Pressed += UseDefaultLibrary;
		GetNode<Button>("Center/VBox/PickButton").Pressed += () => _fileDialog.PopupCentered();
		_fileDialog.FileSelected += OnFileSelected;
		string dir = ProjectSettings.GlobalizePath("res://");
		if (DirAccess.DirExistsAbsolute(dir))
			_fileDialog.CurrentDir = dir;
	}

	/// <summary>使用工程自带的 words.json 进入练习。</summary>
	private void UseDefaultLibrary()
	{
		SelectedWordsPath = "res://words.json";
		GetTree().ChangeSceneToFile("res://practice.tscn");
	}

	/// <summary>校验所选 JSON 有效后记录路径并进入练习；无效则显示提示。</summary>
	private void OnFileSelected(string path)
	{
		if (WordRepository.Load(path).Count == 0)
		{
			_errorLabel.Text = "无法读取该文件，请确认是有效的词库 JSON";
			return;
		}
		SelectedWordsPath = path;
		GetTree().ChangeSceneToFile("res://practice.tscn");
	}
}
