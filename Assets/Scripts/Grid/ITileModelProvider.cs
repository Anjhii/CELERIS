// ============================================================
// ITileModelProvider.cs  |  Assets/Scripts/Grid/
//
// PROPOSITO (DIP):
//   TileComponent depende de esta abstraccion para obtener los
//   prefabs de modelos especiales, en lugar de leerlos de campos
//   static propios.
//
// IMPLEMENTACION:
//   TileModelRegistry implementa esta interfaz y se registra como
//   singleton al inicializarse (Script Execution Order -100).
//   TileComponent.Awake() resuelve el provider una sola vez.
//
// POR QUE NO STATIC:
//   Los statics de prefab en TileComponent son invisibles al
//   Inspector, impiden testeo aislado de TileComponent, y crean
//   dependencia implicita de orden de inicializacion en todo
//   el proyecto. El singleton explicito via interfaz hace la
//   dependencia visible y controlable.
// ============================================================
using UnityEngine;

namespace Celeris.Grid
{
    public interface ITileModelProvider
    {
        GameObject ArrowPrefab  { get; }
        GameObject LaserPrefab  { get; }
        GameObject ChargePrefab { get; }
        GameObject GoalPrefab   { get; }
    }
}
