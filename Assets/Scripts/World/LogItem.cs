using UnityEngine;

namespace ArkSurvival.World
{
    /// <summary>
    /// Полено, выпавшее из срубленного дерева (DINO-039): требует <see cref="Rigidbody"/> и
    /// коллайдер, хранит флаги для будущего хранилища и умеет безопасно уничтожать себя.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class LogItem : MonoBehaviour
    {
        private bool _isDestroyed;

        /// <summary>Полено подобрано и удерживается хранилищем.</summary>
        public bool IsCarried { get; private set; }

        /// <summary>Полено учтено хранилищем; ставится в момент регистрации.</summary>
        public bool IsRegistered { get; private set; }

        /// <summary>Полено уже уничтожено (или уничтожается): повторные вызовы ничего не делают.</summary>
        public bool IsDestroyed => _isDestroyed;

        /// <summary>Хранилище отмечает полено как поднятое или отпущенное.</summary>
        public void SetCarried(bool isCarried)
        {
            if (_isDestroyed)
            {
                return;
            }

            IsCarried = isCarried;
        }

        /// <summary>Хранилище отмечает полено как учтённое или снимает с учёта.</summary>
        public void SetRegistered(bool isRegistered)
        {
            if (_isDestroyed)
            {
                return;
            }

            IsRegistered = isRegistered;
        }

        /// <summary>
        /// Уничтожает полено один раз: повторный вызов и вызов у уже уничтоженного объекта
        /// безопасны и не бросают исключений.
        /// </summary>
        public void DestroyLog()
        {
            if (_isDestroyed)
            {
                return;
            }

            _isDestroyed = true;
            IsCarried = false;
            IsRegistered = false;

            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }
    }
}
