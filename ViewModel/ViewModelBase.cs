using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace word_input.ViewModel;

/// <summary>所有 ViewModel 的基类：实现 INotifyPropertyChanged。</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	/// <summary>一批属性同时变化时逐个通知。</summary>
	protected void Raise(params string[] propertyNames)
	{
		foreach (var name in propertyNames)
			OnPropertyChanged(name);
	}

	protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
			return false;
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}
}
