
using Game.Managers.EntityManager.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(LineRenderer)) ]

public class PlayerMarker : MonoBehaviour
{
    public static PlayerMarker Instance;
    public List<BasePlayer> players = new List<BasePlayer>();
    private LineRenderer espLines;
    private BasePlayer espPlayer;
    [Range(0, 2)]
    public float lineWidth = 1;
    public Color lineColor = Color.red;
    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        espLines = GetComponent<LineRenderer>();
        espLines.positionCount = 2;
        espLines.startWidth = lineWidth;
        espLines.endWidth = lineWidth;
        espLines.material = new Material(Shader.Find("Sprites/Default"));
        espLines.startColor = lineColor;
        espLines.endColor = lineColor;
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
    void Update()
    {
        //will those show all lines like tuanz?
        foreach (BasePlayer player in players)
        {
            if (player.GetIsLocalPlayer() || !espPlayer)
                espPlayer = player;
            if (player == espPlayer)
            {
                continue;
            }

           // DrawLine(espPlayer.transform.position, player.transform.position);
        }
        Vector3 Minposition = players.Where(x => !x.GetIsLocalPlayer()).OrderBy(x => Vector3.Distance(x.transform.position, espPlayer.transform.position)).FirstOrDefault().transform.position;
        DrawLine(espPlayer.transform.position, Minposition);
    }

    public void DrawLine(Vector3 start, Vector3 end)
    {
        espLines.SetPosition(0, start);
        espLines.SetPosition(1, end);
    }

    private void OnGUI()
    {
        //if player is local player, draw a marker above their head with cordinates

        foreach (BasePlayer player in players)
        {
            if (player.GetIsLocalPlayer() || !espPlayer)
                espPlayer = player;
            Vector3 pos = Camera.main.WorldToScreenPoint(player.transform.position + Vector3.up * 2);
            GUI.Label(new Rect(pos.x - 50, Screen.height - pos.y - 25, 100, 50), $"You are here\nX: {player.transform.position.x:F1}\nY: {player.transform.position.y:F1}\nZ: {player.transform.position.z:F1}");
        }

    }
}  