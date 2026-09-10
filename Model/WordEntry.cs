using System.Collections.Generic;

namespace word_input.Model;

/// <summary>words.json 中的一条词条，纯数据，不依赖任何引擎类型。</summary>
public sealed record WordEntry(
	string Name, 
	string UkPhone, 
	string UsPhone, 
	IReadOnlyList<string> Trans
);
