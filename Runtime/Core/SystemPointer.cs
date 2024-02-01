using UnityEngine;
using UnityEngine.EventSystems;

namespace GalaxyGourd.SimulatedPointer
{
    public class SystemPointer : IInputPointer
    {
        #region VARIABLES

        Vector3 IInputPointer.Position => Input.mousePosition;
        bool IInputPointer.IsOverUI => EventSystem.current.IsPointerOverGameObject();
        Camera IInputPointer.Camera => Camera.current;
        InputPointerType IInputPointer.Type => InputPointerType.System;

        #endregion VARIABLES
    }
}