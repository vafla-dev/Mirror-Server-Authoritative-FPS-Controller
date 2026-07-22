


# Mirror-Server-Authoritative-FPS-Controller
Open-source server-authoritative CharacterController for Unity Mirror Networking.

Features:
- ✅ Server authoritative movement
- ✅ Client prediction
- ✅ High ping support (tested up to 200 ms)
- ✅ CharacterController based
- ✅ Anti-cheat friendly
- ✅ Easy integration


# Supported Networking Solutions

- [Mirror](https://github.com/MirrorNetworking/Mirror)


# Info

- [How To Integrate](https://github.com/vafla-dev/Mirror-Server-Authoritative-FPS-Controller/tree/main#how-to-integrate)
- [How It Works](https://github.com/vafla-dev/Mirror-Server-Authoritative-FPS-Controller/tree/main#how-it-works)
- [Customization & Extending Logic](https://github.com/vafla-dev/Mirror-Server-Authoritative-FPS-Controller/tree/main#customization--extending-logic)

# How To Integrate

- [Download](https://github.com/vafla-dev/Mirror-Server-Authoritative-FPS-Controller/archive/refs/heads/main.zip) as a ZIP file.
- Unpack the file and move it into the Unity editor.
- Your project must include [Mirror](https://github.com/MirrorNetworking/Mirror).
- Add the **Character Controller** component to your player prefab.
- Add the [Mirror FPS Controller.cs](https://github.com/vafla-dev/Mirror-Server-Authoritative-FPS-Controller/blob/main/Scripts/Mirror%20FPS%20Controller.cs) component to your player prefab.
- Add your camera's rotation object to the "Rot Main Camera" field.
- example:

https://github.com/user-attachments/assets/d5dc8ba1-9b51-4422-b4bc-3325f917ca59

# Customization & Extending Logic

- Basic knowledge of C# is required for the successful integration of the script into the project.

* **`CenMove()`** – Return `false` to block player movement (e.g., when the inventory, chat, or pause menu is open).
* **`CenRotate()`** – Return `false` to block camera rotation (e.g., during shop interactions or cutscenes).
* **`GravityCanBeCalculated(bool isServer)`** – Return `false` to pause gravity logic. Useful for flight modes, swimming, or admin NoClip mechanics.

To integrate these conditions, create a custom script that inherits from [`Mirror FPS Controller.cs`](https://github.com/vafla-dev/Mirror-Server-Authoritative-FPS-Controller/blob/main/Scripts/Mirror%20FPS%20Controller.cs):
```csharp
using UnityEngine;

public class PlayerControler : MirrorFPSController
{
    public bool fly = true;
    public bool isInventoryOpen = true;
    public bool stunned = true;

    protected override bool CenMove()
    {
        // Player cannot move if inventory is open
        if(isInventoryOpen) return false;

        return true;
    }

    protected override bool CenRotate()
    {
        //The player cannot rotate the camera while stunned
        if (stunned) return false;

        return true;
    }

    protected override bool GravityCanBeCalculated(bool isServer = false)
    {
        //Gravity is disabled for the player if flight mode is enabled
        if(fly) return false;

        return true;
    }
}
```


# How It Works

- This controller is unlike the ones used in popular games.
- The client doesn't pass an input; instead, it passes two positions: its own and the one where it wants to end up:
  ```csharp
  CmdSetMoveTarget(transform.position, transform.position + vector);
  ```
- The server, in turn, does not move the player immediately; it simply stores this data:
   ```csharp
     [Command]
     private void CmdSetMoveTarget(Vector3 corentPos, Vector3 target)
     {
         corentPosForServer = corentPos;
         targetForServer = target;
     }
   ```
- In the next frame, the server does the following:
 - aaaa


<img width="432" height="432" alt="example of work7-22-2026_20-25-30" src="https://github.com/user-attachments/assets/53b20b31-fbde-4f27-9ab0-092a216a7b43" />
