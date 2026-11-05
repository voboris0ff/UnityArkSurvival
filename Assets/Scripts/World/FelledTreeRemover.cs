using UnityEngine;

namespace ArkSurvival.World
{
    /// <summary>
    /// Удаление срубленного дерева (DINO-041): подписывается на <see cref="ChoppableTree.OnFelled"/>,
    /// сразу отключает коллайдеры и рендереры, при необходимости создаёт пенёк из
    /// <c>Stump Prefab</c> и уничтожает объект дерева. Порядок вызовов гарантирован:
    /// <see cref="TreeLogSpawner"/> выполняется раньше (порядок исполнения скриптов), а
    /// <see cref="Object.Destroy(Object)"/> откладывается до конца кадра, поэтому подписчики
    /// события успевают отработать на живом объекте.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(ChoppableTree))]
    public sealed class FelledTreeRemover : MonoBehaviour
    {
        [Tooltip("Префаб пенька, остающегося на месте дерева. Пусто — пенёк не создаётся.")]
        [SerializeField] private GameObject _stumpPrefab;

        private Collider[] _colliders;
        private Renderer[] _renderers;
        private ChoppableTree _tree;
        private bool _isRemoving;

        private void Awake()
        {
            _tree = GetComponent<ChoppableTree>();

            // Ссылки кэшируются до отключения, чтобы не искать компоненты в обработчике события.
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void OnEnable()
        {
            if (_tree != null)
            {
                _tree.OnFelled += OnTreeFelled;
            }
        }

        private void OnDisable()
        {
            if (_tree != null)
            {
                _tree.OnFelled -= OnTreeFelled;
            }
        }

        private void OnTreeFelled(ChoppableTree tree, Vector3 source)
        {
            if (_isRemoving)
            {
                return;
            }

            _isRemoving = true;

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = false;
                }
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = false;
                }
            }

            if (_stumpPrefab != null)
            {
                Transform treeTransform = tree.transform;
                Instantiate(_stumpPrefab, treeTransform.position, treeTransform.rotation);
            }

            Destroy(tree.gameObject);
        }
    }
}
