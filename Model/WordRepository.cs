using Godot;
using System.Collections.Generic;

namespace word_input.Model;

public interface IWordRepository
{
	/// <summary>读取全部词条；读取或解析失败时返回空列表。</summary>
	IReadOnlyList<WordEntry> Load();
}

/// <summary>从 res:// 下的 JSON 文件加载词条。</summary>
public sealed class WordRepository : IWordRepository
{
	private readonly string _path;

	public WordRepository(string path) => _path = path;

	public IReadOnlyList<WordEntry> Load()
	{
		using var file = FileAccess.Open(_path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PushError($"无法打开 {_path}");
			return [];
		}
		var data = Json.ParseString(file.GetAsText());
		if (data.VariantType != Variant.Type.Array)
			return [];
		var words = new List<WordEntry>();
		foreach (var item in data.AsGodotArray())
		{
			if (item.VariantType != Variant.Type.Dictionary)
				continue;
			var entry = item.AsGodotDictionary();
			var trans = new List<string>();
			if (entry.TryGetValue("trans", out var transVariant) && transVariant.VariantType == Variant.Type.Array)
			{
				foreach (var line in transVariant.AsGodotArray())
					trans.Add(line.AsString());
			}
			words.Add(new WordEntry(
				entry.TryGetValue("name", out var name) ? name.AsString() : "",
				entry.TryGetValue("ukphone", out var uk) ? uk.AsString() : "",
				entry.TryGetValue("usphone", out var us) ? us.AsString() : "",
				trans));
		}
		return words;
	}
}
