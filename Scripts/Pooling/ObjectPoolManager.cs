using Godot;
using System.Collections.Generic;

public partial class ObjectPoolManager : Node
{
#region Singleton
	public static ObjectPoolManager Singleton { get; private set; }
#endregion

#region Exports
	[Export] public int DefaultPrewarmCount = 0;
	[Export] public Node PoolRoot;
#endregion

#region State
	private readonly Dictionary<string, Pool> pools = new Dictionary<string, Pool>();
	private const string PoolKeyMeta = "_pool_key";
#endregion

#region Lifecycle
	public override void _EnterTree()
	{
		if (Singleton == null)
		{
			Singleton = this;
		}
		else
		{
			QueueFree();
		}
	}

	public override void _Ready()
	{
		if (PoolRoot == null)
		{
			PoolRoot = new Node();
			PoolRoot.Name = "PoolRoot";
			AddChild(PoolRoot);
		}
	}
#endregion

#region PublicApi
	public void Prewarm(PackedScene scene, int count)
	{
		if (scene == null || count <= 0)
		{
			return;
		}

		Pool pool = GetOrCreatePool(scene);
		for (int i = 0; i < count; i++)
		{
			Node node = CreateInstance(scene);
			ReturnToPool(pool, node);
		}
	}

	public T Spawn<T>(PackedScene scene, Node parent = null) where T : Node
	{
		return Spawn(scene, parent) as T;
	}

	public Node Spawn(PackedScene scene, Node parent = null)
	{
		if (scene == null)
		{
			return null;
		}

		Pool pool = GetOrCreatePool(scene);
		Node node = pool.Inactive.Count > 0 ? pool.Inactive.Pop() : CreateInstance(scene);

		ActivateNode(node, parent ?? this);
		return node;
	}

	public bool Release(Node node)
	{
		if (node == null || !GodotObject.IsInstanceValid(node))
		{
			return false;
		}

		string key = GetPoolKey(node);
		if (string.IsNullOrEmpty(key))
		{
			node.QueueFree();
			return false;
		}

		if (!pools.TryGetValue(key, out Pool pool))
		{
			PackedScene scene = GD.Load<PackedScene>(key);
			if (scene == null)
			{
				node.QueueFree();
				return false;
			}
			pool = GetOrCreatePool(scene);
		}

		ReturnToPool(pool, node);
		return true;
	}
#endregion

#region Internals
	private Pool GetOrCreatePool(PackedScene scene)
	{
		string key = GetPoolKey(scene);

		if (!pools.TryGetValue(key, out Pool pool))
		{
			pool = new Pool(scene);
			pools.Add(key, pool);

			if (DefaultPrewarmCount > 0)
			{
				Prewarm(scene, DefaultPrewarmCount);
			}
		}

		return pool;
	}

	private Node CreateInstance(PackedScene scene)
	{
		Node node = scene.Instantiate();
		string key = GetPoolKey(scene);
		node.SetMeta(PoolKeyMeta, key);
		return node;
	}

	private void ActivateNode(Node node, Node parent)
	{
		if (node.GetParent() != parent)
		{
			node.GetParent()?.RemoveChild(node);
			parent.AddChild(node);
		}

		node.ProcessMode = Node.ProcessModeEnum.Inherit;

		if (node is CanvasItem canvasItem)
		{
			canvasItem.Visible = true;
		}
		else if (node is Node3D node3D)
		{
			node3D.Visible = true;
		}

		if (node is IPoolable poolable)
		{
			poolable.OnAcquireFromPool();
		}
	}

	private void ReturnToPool(Pool pool, Node node)
	{
		if (node is IPoolable poolable)
		{
			poolable.OnReleaseToPool();
		}

		if (PoolRoot != null && node.GetParent() != PoolRoot)
		{
			node.GetParent()?.RemoveChild(node);
			PoolRoot.AddChild(node);
		}

		node.ProcessMode = Node.ProcessModeEnum.Disabled;

		if (node is CanvasItem canvasItem)
		{
			canvasItem.Visible = false;
		}
		else if (node is Node3D node3D)
		{
			node3D.Visible = false;
		}

		pool.Inactive.Push(node);
	}

	private string GetPoolKey(PackedScene scene)
	{
		if (scene == null)
		{
			return string.Empty;
		}

		if (!string.IsNullOrEmpty(scene.ResourcePath))
		{
			return scene.ResourcePath;
		}

		return scene.GetInstanceId().ToString();
	}

	private string GetPoolKey(Node node)
	{
		if (node.HasMeta(PoolKeyMeta))
		{
			return node.GetMeta(PoolKeyMeta).AsString();
		}

		if (!string.IsNullOrEmpty(node.SceneFilePath))
		{
			return node.SceneFilePath;
		}

		return string.Empty;
	}
#endregion

#region Data
	private class Pool
	{
		public PackedScene Scene { get; }
		public Stack<Node> Inactive { get; } = new Stack<Node>();

		public Pool(PackedScene scene)
		{
			Scene = scene;
		}
	}
#endregion
}
