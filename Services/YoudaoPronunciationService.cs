using Godot;
using System.Collections.Generic;

namespace word_input.Services;

public interface IPronunciationService
{
	/// <summary>朗读单词；已缓存时立即播放，否则下载完成后播放。</summary>
	void Speak(string word);

	/// <summary>后台预取单词发音，只入缓存不出声。</summary>
	void Prefetch(string word);

	/// <summary>停止播放并取消等待中的播放。</summary>
	void Stop();
}

/// <summary>有道词典发音服务：dictvoice 音源 + user:// 本地缓存。</summary>
public sealed class YoudaoPronunciationService : IPronunciationService
{
	private const string VoiceUrlFormat = "https://dict.youdao.com/dictvoice?audio={0}&type=2";
	private const string VoiceCacheDir = "user://voice_cache";

	private readonly Node _parent;
	private readonly AudioStreamPlayer _player;
	private readonly HashSet<string> _downloading = new();
	private string _playOnArrive;

	/// <param name="parent">用于挂载播放器与下载节点的场景节点，由 View 提供。</param>
	public YoudaoPronunciationService(Node parent)
	{
		_parent = parent;
		_player = new AudioStreamPlayer();
		parent.AddChild(_player);
		DirAccess.MakeDirRecursiveAbsolute(VoiceCacheDir);
	}

	public void Speak(string word) => Request(word, play: true);

	public void Prefetch(string word) => Request(word, play: false);

	public void Stop()
	{
		_playOnArrive = null;
		_player.Stop();
	}

	private void Request(string word, bool play)
	{
		if (string.IsNullOrEmpty(word))
			return;
		string cachePath = $"{VoiceCacheDir}/{word}.mp3";
		if (FileAccess.FileExists(cachePath))
		{
			if (play)
				PlayFromCache(cachePath);
			return;
		}
		if (play)
			_playOnArrive = word;
		if (!_downloading.Add(word))
			return; // 同一词已有请求在途
		var http = new HttpRequest { Timeout = 10 };
		_parent.AddChild(http);
		http.RequestCompleted += (result, responseCode, headers, body) =>
		{
			http.QueueFree();
			_downloading.Remove(word);
			bool ok = result == (long)HttpRequest.Result.Success && responseCode >= 200
				&& responseCode < 300 && body != null && body.Length > 0;
			if (ok)
			{
				using var writer = FileAccess.Open(cachePath, FileAccess.ModeFlags.Write);
				writer?.StoreBuffer(body);
			}
			else
			{
				GD.PushWarning($"朗读音频下载失败：{word}（HTTP {responseCode}）");
			}
			// 仅当该词仍是最近一次请求朗读的词时才出声
			if (ok && _playOnArrive == word)
			{
				_playOnArrive = null;
				PlayBytes(body);
			}
			else if (!ok && _playOnArrive == word)
			{
				_playOnArrive = null;
			}
		};
		http.Request(string.Format(VoiceUrlFormat, word));
	}

	private void PlayFromCache(string cachePath)
	{
		using var file = FileAccess.Open(cachePath, FileAccess.ModeFlags.Read);
		PlayBytes(file?.GetBuffer((long)file.GetLength()));
	}

	private void PlayBytes(byte[] data)
	{
		if (data == null || data.Length == 0)
			return;
		var mp3 = new AudioStreamMP3 { Data = data };
		_player.Stream = mp3;
		_player.Play();
	}
}
