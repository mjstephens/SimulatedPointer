using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace GalaxyGourd.SimulatedPointer
{
    /// <summary>
    /// Translates mouse or gamepad inputs to an on-screen pointer.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SimulatedPointer : MonoBehaviour, IInputPointer
    {
        #region VARIABLES

        [Header("Input")] 
        [SerializeField] private InputAction _actionDelta;
        [SerializeField] private InputAction _actionScroll;
        [SerializeField] private InputAction _actionSelect;
        [SerializeField] private InputAction _actionSelectAlternate;
        [SerializeField] private InputAction _actionSelectTertiary;
        [SerializeField] private float _pointerSpeedMouseKB;
        [SerializeField] private float _pointerSpeedGamepad;
        
        InputPointerType IInputPointer.Type => InputPointerType.Simulated;
        public Vector3 Position => Rect.position;
        public bool IsOverUI => HoveredObjects.Count > 0;
        public RectTransform Rect { get; private set; }
        public Vector2 Delta => _dataInput.Delta;
        public Camera Camera => _uiCamera;
        public List<GameObject> HoveredObjects { get; private set; } = new();

        private Camera _uiCamera;
        private EventSystem _eventSystem;
        private RectTransform _pointerOverlay;
        private PointerEventData _eventData;
        private List<RaycastResult> _raycastResults = new();
        private readonly List<GameObject> _selectedObjs = new();
        private DataInputValuesPointer _dataInput;
        private readonly List<IPointerDataReceiver> _receivers = new();
        private Vector2 _prevEventPos;
        private bool _isGamepad;

        #endregion VARIABLES


        #region INITIALIZATION

        private void Awake()
        {
            Rect = GetComponent<RectTransform>();
            _eventData = new PointerEventData(_eventSystem);
            _pointerOverlay = transform.parent.GetComponent<RectTransform>();
            _eventSystem = FindObjectOfType<EventSystem>();
            if (!_eventSystem)
            {
                _eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
                _eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
            
            // Try and get camera
            Canvas canvas = GetComponentInParent<Canvas>();
            _uiCamera = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
        }

        private void OnEnable()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            
            _actionDelta.Enable();
            _actionScroll.Enable();
            _actionSelect.Enable();
            _actionSelectAlternate.Enable();
            _actionSelectTertiary.Enable();
            
            _actionSelect.started += OnActionSelectStart;
            _actionSelect.performed += OnActionSelectPerformed;
            _actionSelect.canceled += OnActionSelectCanceled;
            
            _actionSelectAlternate.started += OnActionSelectAlternateStart;
            _actionSelectAlternate.performed += OnActionSelectAlternatePerformed;
            _actionSelectAlternate.canceled += OnActionSelectAlternateCanceled;
            
            _actionSelectTertiary.started += OnActionSelectTertiaryStart;
            _actionSelectTertiary.performed += OnActionSelectTertiaryPerformed;
            _actionSelectTertiary.canceled += OnActionSelectTertiaryCanceled;
        }

        private void OnDisable()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            
            _actionSelect.started -= OnActionSelectStart;
            _actionSelect.performed -= OnActionSelectPerformed;
            _actionSelect.canceled -= OnActionSelectCanceled;
            
            _actionSelectAlternate.started -= OnActionSelectAlternateStart;
            _actionSelectAlternate.performed -= OnActionSelectAlternatePerformed;
            _actionSelectAlternate.canceled -= OnActionSelectAlternateCanceled;
            
            _actionSelectTertiary.started -= OnActionSelectTertiaryStart;
            _actionSelectTertiary.performed -= OnActionSelectTertiaryPerformed;
            _actionSelectTertiary.canceled -= OnActionSelectTertiaryCanceled;
            
            _actionDelta.Disable();
            _actionScroll.Disable();
            _actionSelect.Disable();
            _actionSelectAlternate.Disable();
            _actionSelectTertiary.Disable();
        }

        #endregion INITIALIZATION


        #region INPUT

        private void Update()
        {
            float dt = Time.deltaTime;
            _isGamepad = _actionDelta.activeControl?.device is Gamepad;
            
            // Move pointer
            Vector2 delta = _actionDelta.ReadValue<Vector2>();
            _dataInput.Scroll = _actionScroll.ReadValue<Vector2>();
            float speed = _isGamepad ? _pointerSpeedGamepad * dt : _pointerSpeedMouseKB * dt;
            Vector2 increase = speed * delta;
            _dataInput.Delta = increase;
            Rect.anchoredPosition = GatePointerPosition(Rect.anchoredPosition + increase);
            _dataInput.Position = Rect.position;
            _eventData.delta = increase;

            // Move virtual mouse
            _eventData.position = _uiCamera == null ? Rect.position : _uiCamera.WorldToScreenPoint(Rect.position);
            _raycastResults = new List<RaycastResult>();
            _eventSystem.RaycastAll(_eventData, _raycastResults);
            
            // Update hovered objects
            List<GameObject> newHovered = new List<GameObject>();
            foreach (RaycastResult obj in _raycastResults)
            {
                newHovered.Add(obj.gameObject);
                _eventData.pointerCurrentRaycast = obj;
            }

            ResolveHoveredObjects(newHovered);

            if (_prevEventPos != _eventData.position)
            {
                foreach (GameObject obj in HoveredObjects)
                {
                    Move(obj);
                }
            }
            _prevEventPos = _eventData.position;
        }

        private void OnActionSelectStart(InputAction.CallbackContext context)
        {
            _eventData.button = PointerEventData.InputButton.Left;
            _dataInput.SelectStarted = true;
            _dataInput.SelectIsPressed = true;
                
            foreach (RaycastResult obj in _raycastResults)
            {
                _eventData.pointerPressRaycast = obj;
            }
                
            ResolveActionPointerDown();
        }
        
        private void OnActionSelectPerformed(InputAction.CallbackContext context)
        {
            _eventData.button = PointerEventData.InputButton.Left;
        }

        private void OnActionSelectCanceled(InputAction.CallbackContext context)
        {
            _dataInput.SelectReleased = true;
            _dataInput.SelectIsPressed = false;
            ResolveActionPointerUp();
        }
        
        private void OnActionSelectAlternateStart(InputAction.CallbackContext context)
        {
            _eventData.button = PointerEventData.InputButton.Right;
            _dataInput.SelectAlternateStarted = true;
            _dataInput.SelectAlternateIsPressed = true;
            ResolveActionPointerDown();
        }
        
        private void OnActionSelectAlternatePerformed(InputAction.CallbackContext context)
        {
            _eventData.button = PointerEventData.InputButton.Right;
            _eventData.button = PointerEventData.InputButton.Left;
        }

        private void OnActionSelectAlternateCanceled(InputAction.CallbackContext context)
        {
            _eventData.button = PointerEventData.InputButton.Right;
            _dataInput.SelectAlternateReleased = true;
            _dataInput.SelectAlternateIsPressed = false;
            ResolveActionPointerUp();
        }

        private void OnActionSelectTertiaryStart(InputAction.CallbackContext context)
        {
            _eventData.button = PointerEventData.InputButton.Middle;
            ResolveActionPointerDown();
        }
        
        private void OnActionSelectTertiaryPerformed(InputAction.CallbackContext context)
        {
            _eventData.button = PointerEventData.InputButton.Middle;
            _eventData.button = PointerEventData.InputButton.Right;
            _eventData.button = PointerEventData.InputButton.Left;
        }

        private void OnActionSelectTertiaryCanceled(InputAction.CallbackContext context)
        {
            _eventData.button = PointerEventData.InputButton.Middle;
            ResolveActionPointerUp();
        }
        
        #endregion INPUT


        #region EVENTS

        private void ResolveHoveredObjects(List<GameObject> objs)
        {
            foreach (GameObject obj in objs)
            {
                if (!HoveredObjects.Contains(obj))
                {
                    Enter(obj);
                }
            }

            foreach (GameObject obj in HoveredObjects)
            {
                if (!objs.Contains(obj))
                {
                    Exit(obj);
                }
            }

            HoveredObjects = objs;
        }

        private void ResolveActionPointerDown()
        {
            foreach (GameObject obj in HoveredObjects) 
            {
                PointerDown(obj);
                _selectedObjs.Add(obj);
            }
        }

        private void ResolveActionPointerUp()
        {
            foreach (GameObject obj in _selectedObjs)
            {
                PointerUp(obj);
            }

            _selectedObjs.Clear();
        }

        private void Enter(GameObject obj)
        {
            ExecuteEvents.Execute(obj, _eventData, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(obj, _eventData, ExecuteEvents.initializePotentialDrag);
        }

        private void Exit(GameObject obj)
        {
            ExecuteEvents.Execute(obj, _eventData, ExecuteEvents.pointerExitHandler);
        }

        private void PointerDown(GameObject obj)
        {
            ExecuteEvents.Execute(obj, _eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(obj, _eventData, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(obj, _eventData, ExecuteEvents.beginDragHandler);
        }

        private void PointerUp(GameObject obj)
        {
            ExecuteEvents.Execute(obj, _eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(obj, _eventData, ExecuteEvents.deselectHandler);
            ExecuteEvents.Execute(obj, _eventData, ExecuteEvents.endDragHandler);
            ExecuteEvents.Execute(obj, _eventData, ExecuteEvents.cancelHandler);
        }

        private void Move(GameObject obj)
        {
            ExecuteEvents.Execute(obj, _eventData, ExecuteEvents.pointerMoveHandler);
            if (_dataInput.SelectIsPressed)
            {
                ExecuteEvents.Execute(obj, _eventData, ExecuteEvents.dragHandler);
            }
        }

        #endregion EVENTS


        #region TRANSMIT

        private void LateUpdate()
        {
            foreach (IPointerDataReceiver receiver in _receivers)
            {
                receiver.ReceiveInput(_dataInput);
            }
            
            // Reset input
            _dataInput.SelectStarted = false;
            _dataInput.SelectReleased = false;
            _dataInput.SelectAlternateStarted = false;
            _dataInput.SelectAlternateReleased = false;
        }

        public void RegisterReceiver(IPointerDataReceiver receiver)
        {
            _receivers.Add(receiver);
        }
        
        public void UnregisterReceiver(IPointerDataReceiver receiver)
        {
            _receivers.Remove(receiver);
        }

        #endregion TRANSMIT


        #region UTILITY

        public void SnapPointerToPosition(Vector2 pos)
        {
            Rect.anchoredPosition = pos;
            _dataInput.Position = Rect.position;
            
            _eventData.position = _uiCamera.WorldToScreenPoint(Rect.position);
            _prevEventPos = _eventData.position;
        }

        private Vector2 GatePointerPosition(Vector2 inPosition)
        {
            Vector2 adjustment = Vector2.zero;
            if (inPosition.x < _pointerOverlay.rect.xMin)
                adjustment.x = _pointerOverlay.rect.xMin - inPosition.x;
            else if (inPosition.x > _pointerOverlay.rect.xMax)
                adjustment.x = _pointerOverlay.rect.xMax - inPosition.x;
            
            if (inPosition.y < _pointerOverlay.rect.yMin)
                adjustment.y = _pointerOverlay.rect.yMin - inPosition.y;
            else if (inPosition.y > _pointerOverlay.rect.yMax)
                adjustment.y = _pointerOverlay.rect.yMax - inPosition.y;
            
            return inPosition + adjustment;
        }

        #endregion UTILITY
    }
}