#region Pooling
public interface IPoolable
{
	void OnAcquireFromPool();
	void OnReleaseToPool();
}
#endregion
