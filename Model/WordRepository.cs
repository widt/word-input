using Godot;
using System.Collections.Generic;

namespace word_input.Model;

/// <summary>从 res:// 下的 JSON 文件加载词条。</summary>
public static class WordRepository
{
	/// <summary>解析 JSON 并返回全部词条；文件缺失或格式不对时返回空列表。</summary>
	public static List<WordEntry> Load(string path)
	{
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PushError($"无法打开 {path}");
			return [];
		}
		var data = Json.ParseString(file.GetAsText());
		var words = new List<WordEntry>();
		if (data.VariantType != Variant.Type.Array)
			return words;
		foreach (var item in data.AsGodotArray())
		{
			if (item.VariantType != Variant.Type.Dictionary)
				continue;
			var e = item.AsGodotDictionary();
			var trans = new List<string>();
			if (e.TryGetValue("trans", out var t) && t.VariantType == Variant.Type.Array)
			{
				foreach (var line in t.AsGodotArray())
					trans.Add(line.AsString());
			}
			words.Add(new WordEntry(
				e.TryGetValue("name", out var name) ? name.AsString() : "",
				e.TryGetValue("ukphone", out var uk) ? uk.AsString() : "",
				e.TryGetValue("usphone", out var us) ? us.AsString() : "",
				trans));
		}
		return words;
	}
}
