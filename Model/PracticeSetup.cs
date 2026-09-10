using System.Collections.Generic;

namespace word_input.Model;

/// <summary>词库选择场景传给练习场景的一次性配置。</summary>
public static class PracticeSetup
{
	/// <summary>词库 JSON 路径；null 表示未经选择场景，使用默认词库。</summary>
	public static string WordsPath;

	/// <summary>是否随机排序。</summary>
	public static bool RandomOrder = true;

	/// <summary>选中的词条索引；null 表示全部词。</summary>
	public static List<int> Indices;

	/// <summary>按配置加载并筛选词条，随后复位一次性状态。</summary>
	public static List<WordEntry> Take()
	{
		var words = WordRepository.Load(WordsPath ?? "res://words.json");
		WordsPath = null;
		if (Indices == null)
			return words;
		var picked = new List<WordEntry>();
		foreach (var i in Indices)
		{
			if (i >= 0 && i < words.Count)
				picked.Add(words[i]);
		}
		Indices = null;
		return picked;
	}
}
