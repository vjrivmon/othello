using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{
    //GameObjects de la escena
    public GameObject boardGameObject;
    public GameObject player1;
    public GameObject player2;
    public GameObject piecePrefab;
    public GameObject lastMovementPrefab;

    //UI
    public Text finalMessage;
    public Text blackText;
    public Text whiteText;
    public Button playAgainButton;

    //Materials
    public Material blackMaterial;
    public Material whiteMaterial;
    public Material tileMaterial;
    public Material tileSelectableMaterial;

    // Variable para el retardo del movimiento de la IA (en segundos)
    public float aiMoveDelay = 1.5f; 

    //Otras variables
    GameObject[] tileGameObjects = new GameObject[Constants.NumTiles];
    GameObject[] pieces = new GameObject[Constants.NumTiles];
    Tile[] board = new Tile[Constants.NumTiles];
    private List<int> selectableTiles = new List<int>();
    private BoardManager boardManager;
    GameObject lastMovementMark;
    private int turn;
    private bool passBlack = false, passWhite = false;
                    
    void Start()
    {       
        InitTiles();

        boardManager = GameObject.FindGameObjectWithTag("BoardManager").GetComponent<BoardManager>();

        turn = Constants.Start;
        PlaceInitialPiece(3, 3, turn);
        PlaceInitialPiece(4, 4, turn);
        PlaceInitialPiece(3, 4, -turn);
        PlaceInitialPiece(4, 3, -turn);

        DrawSelectableTiles();

        Vector3 pos = new Vector3(-1.5f, -0.138f, -1.5f);
        lastMovementMark = Instantiate(lastMovementPrefab, pos, Quaternion.identity);

        if (Constants.Player1.CompareTo("AI") == 0 && Constants.Player2.CompareTo("AI") == 0)
        {
            StartCoroutine(CountDown(3));
        }

    }

    //Rellenamos el array de casillas y posicionamos las fichas
    void InitTiles()
    {
        for (int fil = 0; fil < Constants.TilesPerRow; fil++)
        {
            GameObject rowchild = boardGameObject.transform.GetChild(fil).gameObject;

            for (int col = 0; col < Constants.TilesPerRow; col++)
            {
                GameObject tilechild = rowchild.transform.GetChild(col).gameObject;
                tileGameObjects[fil * Constants.TilesPerRow + col] = tilechild;
                board[fil * Constants.TilesPerRow + col] = tileGameObjects[fil * Constants.TilesPerRow + col].GetComponent<TileBehaviour>().tileInfo;
            }
        }

    }

    void PlaceInitialPiece(int row, int col, int color)
    {

        float coordrow = row - 3.5f;
        float coordcol = col - 3.5f;
        int clickedtile = row * 8 + col;

        Vector3 pos = new Vector3(coordcol, 0.238f, coordrow);

        pieces[clickedtile] = Instantiate(piecePrefab, pos, Quaternion.identity);
        if (color == 1)
            pieces[clickedtile].GetComponent<Renderer>().material = blackMaterial;
        else
            pieces[clickedtile].GetComponent<Renderer>().material = whiteMaterial;

        tileGameObjects[clickedtile].GetComponent<TileBehaviour>().tileInfo.value = color;

        blackText.text = ""+ boardManager.CountPieces(board, Constants.Black);
        whiteText.text = "" + boardManager.CountPieces(board, Constants.White);

    }          

    private void DrawSelectableTiles()
    {
        // Reseteamos color
        foreach (int t in selectableTiles)
        {
            tileGameObjects[t].GetComponent<Renderer>().material = tileMaterial;
        }

        // Limpiamos la lista
        selectableTiles.Clear();

        // La rellenamos con las nuevas casillas seleccionables
        selectableTiles = boardManager.FindSelectableTiles(board, turn);
        // Marcamos los seleccionables
        foreach (int t in selectableTiles)
        {
            tileGameObjects[t].GetComponent<Renderer>().material = tileSelectableMaterial;
        }

    }
    
    private bool IsClickable(int t)
    {
        foreach(int s in selectableTiles)
        {
            if (t == s)
                return true;
        }
        return false;
    }
        
    public void ClickOnTile(int clickedtile)
    {                
        if (IsClickable(clickedtile))
        {
            int row = clickedtile / 8;
            int col = clickedtile % 8;
            float coordrow = row - 3.5f;
            float coordcol = col - 3.5f;

            Vector3 pos = new Vector3(coordcol, 0.238f, coordrow);
            pieces[clickedtile] = Instantiate(piecePrefab, pos, Quaternion.identity);

            if (turn == 1)
                pieces[clickedtile].GetComponent<Renderer>().material = blackMaterial;
            else
                pieces[clickedtile].GetComponent<Renderer>().material = whiteMaterial;

            lastMovementMark.transform.position = pos + Vector3.up*0.1f;
            board[clickedtile].value = turn;
            if (turn == Constants.Black)
            {
                passBlack = false;
            }
            else
            {
                passWhite = false;
            }

            //Cambio las fichas con movimiento
            List<int> swappablePieces = boardManager.FindSwappablePieces(board, clickedtile, turn);

            foreach (int s in swappablePieces)
            {
                if (turn == Constants.Black)
                    pieces[s].GetComponent<Renderer>().material = blackMaterial;
                else
                    pieces[s].GetComponent<Renderer>().material = whiteMaterial;

                board[s].value = turn;
            }

            int blackPieces = boardManager.CountPieces(board, Constants.Black);
            int whitePieces = boardManager.CountPieces(board, Constants.White);
            blackText.text = "" + blackPieces;
            whiteText.text = "" + whitePieces;

            if (((blackPieces + whitePieces) == Constants.NumTiles) || blackPieces == 0 || whitePieces == 0)
            {
                finalMessage.text = "End Game!!";
                DrawSelectableTiles();
                int res=GetWinner(blackPieces, whitePieces);
                EndGame(res);
            }

            else
            {
                turn = -turn;                

                DrawSelectableTiles();                

                if (selectableTiles.Count == 0)
                {
                    if (turn == Constants.Black)
                    {
                        passBlack = true;
                        if (passBlack && passWhite)
                        {
                            finalMessage.text = "End Game!!";
                        }
                        else
                        {
                            finalMessage.text = "Pass!!";
                            StartCoroutine("Wait2");
                        }
                    }
                    else
                    {
                        passWhite = true;
                        if (passBlack && passWhite)
                        {
                            finalMessage.text = "End Game!!";
                        }
                        else
                        {
                            finalMessage.text = "Pass!!";
                            StartCoroutine("Wait2");
                        }
                    }

                }

                else
                {
                    NextTurn();
                }
            }                        

        }                      

    }

    public void NextTurn()
    {
        if (turn == 1) // Turno de Negras (Player 1)
        {
            if (Constants.Player1.Equals("AI"))
            {
                StartCoroutine(ProcessAIMove(player1));
            }
            // Si Player1 es humano, no hace nada aquí; espera el clic en ClickOnTile.
        }
        else // Turno de Blancas (Player 2)
        {
            if (Constants.Player2.Equals("AI"))
            {
                StartCoroutine(ProcessAIMove(player2));
            }
            // Si Player2 es humano, no hace nada aquí; espera el clic en ClickOnTile.
        }
    }

    /// <summary>
    /// Corutina para procesar el movimiento de la IA con un retardo.
    /// </summary>
    /// <param name="aiPlayerObject">El GameObject del jugador IA.</param>
    IEnumerator ProcessAIMove(GameObject aiPlayerObject)
    {
        // Mostrar algún mensaje o indicador de que la IA está "pensando" es opcional,
        // ya que el propio cálculo de Minimax en Player.cs puede llevar tiempo.
        // finalMessage.text = "IA está pensando..."; // Ejemplo

        // Obtener el movimiento de la IA.
        // Esta llamada puede tardar si la profundidad de Minimax es alta.
        int tile = aiPlayerObject.GetComponent<PlayerVicenteRivasMonferrer>().SelectTile(board);

        if (tile != -1)
        {
            // Mensaje opcional indicando que la IA ha decidido y va a esperar.
            // finalMessage.text = "IA ha decidido. Aplicando movimiento en " + aiMoveDelay + "s...";
            Debug.Log("IA (" + aiPlayerObject.name + ") ha decidido mover a " + tile + ". Aplicando en " + aiMoveDelay + "s.");

            // Esperar el tiempo definido antes de aplicar el movimiento.
            yield return new WaitForSeconds(aiMoveDelay);

            // Aplicar el movimiento.
            ClickOnTile(tile);
        }
        else
        {
            // Si la IA no puede mover (SelectTile devolvió -1), esto ya se maneja
            // dentro de ClickOnTile o la lógica de cambio de turno que sigue a la ausencia de selectableTiles.
            // Sin embargo, es bueno registrarlo.
            Debug.Log("IA (" + aiPlayerObject.name + ") no tiene movimientos válidos (paso gestionado por la lógica de turnos).");
            // La lógica de pase ya está en ClickOnTile cuando selectableTiles.Count == 0 después de un turno.
            // Aquí simplemente no llamamos a ClickOnTile si tile es -1.
            // El flujo para manejar un "paso" de la IA si no hay movimientos
            // se gestiona a través de la secuencia:
            // 1. IA devuelve -1 (o un movimiento válido).
            // 2. Si el movimiento es válido, se llama ClickOnTile(tile).
            // 3. ClickOnTile actualiza el tablero, luego llama a DrawSelectableTiles para el *siguiente* jugador.
            // 4. Si para el siguiente jugador selectableTiles.Count == 0, se activa la lógica de pase (StartCoroutine("Wait2")).
            // Por lo tanto, si la IA (SelectTile) devuelve -1, y nosotros *no* llamamos a ClickOnTile,
            // el estado de "no hay movimientos para el siguiente" no se evaluará correctamente.
            // La IA debería *siempre* devolver un movimiento válido si hay alguno.
            // Si SelectTile devuelve -1, significa que la IA (según su propia lógica) no encontró movimientos.
            // Esto se manejaría en la parte de ClickOnTile que gestiona cuando no hay selectableTiles.
            // Para evitar problemas, si la IA devuelve -1, podríamos forzar la lógica de pase.
            // Pero es mejor que la lógica de pase en ClickOnTile maneje esto globalmente.
            // La IA en Player.cs ya devuelve -1 si no hay movimientos.
            // Aquí, si tile es -1, no llamamos a ClickOnTile. El juego esperará la interacción del
            // jugador humano o la siguiente llamada a NextTurn si es IA vs IA.
            // Esto requiere que la lógica de pase después de DrawSelectableTiles se active.

            // Si la IA no tiene movimientos (tile == -1), llamamos directamente a la lógica de fin de turno sin acción.
             turn = -turn; // Cambiar turno manualmente.
             finalMessage.text = (turn == Constants.Black ? "Blancas" : "Negras") + " no puede mover. Pasa."; // Indicar el pase.
             DrawSelectableTiles(); // Ver si el siguiente jugador puede mover.
             if (selectableTiles.Count == 0) // Si el siguiente tampoco puede
             {
                 if (this.turn == Constants.Black) passBlack = true; else passWhite = true;
                 if (passBlack && passWhite) {
                     finalMessage.text = "End Game!! Ambos pasan.";
                     int res=GetWinner(boardManager.CountPieces(board, Constants.Black), boardManager.CountPieces(board, Constants.White));
                     EndGame(res);
                 } else {
                     // No iniciar Wait2 aquí, sino llamar a NextTurn para que el otro jugador (posiblemente otra IA) tenga su oportunidad
                     // o para que el flujo continúe.
                     // La corrutina Wait2 se usa para el mensaje "Pass!!" y luego cambia el turno.
                     // Aquí, ya hemos cambiado el turno.
                     NextTurn(); // Dejar que el siguiente jugador actúe o que la lógica de juego continúe.
                 }
             }
             else
             {
                 NextTurn(); // El siguiente jugador tiene movimientos.
             }
        }
    }

    int GetWinner(int blackPieces, int whitePieces)
    {
        if (blackPieces > whitePieces)
            return Constants.Black;
        if (whitePieces > blackPieces)
            return Constants.White;

        return Constants.Empty;
    }

    public void EndGame(int turn)
    {
        if (turn == Constants.Black)
            finalMessage.text = "Black wins!";
        if (turn == Constants.White)
            finalMessage.text = "White wins!";
        if (turn == Constants.Empty)
            finalMessage.text = "Draw!";

        playAgainButton.interactable = true;
    }

    public void PlayAgain()
    {

        GameObject[] pieces = GameObject.FindGameObjectsWithTag("Piece");
        foreach (GameObject g in pieces)
            GameObject.Destroy(g);

        for (int row = 0; row < Constants.TilesPerRow; row++)
            for(int col=0; col < Constants.TilesPerRow; col++)
                board[row*8+col].value = 0;

        playAgainButton.interactable = false;
        finalMessage.text = "";

        turn = Constants.Start;
        PlaceInitialPiece(3, 3, turn);
        PlaceInitialPiece(4, 4, turn);
        PlaceInitialPiece(3, 4, -turn);
        PlaceInitialPiece(4, 3, -turn);

        DrawSelectableTiles();

        Vector3 pos = new Vector3(-1.5f, -0.138f, -1.5f);
        lastMovementMark.transform.position = pos;

        if (Constants.Player1.CompareTo("AI") == 0 && Constants.Player2.CompareTo("AI") == 0)
            StartCoroutine(CountDown(3));

    }
        

    IEnumerator CountDown(int val)
    {

        yield return new WaitForSeconds(1);
        finalMessage.text = "" + val;
        if (val >= 0)
        {
            val--;
            StartCoroutine(CountDown(val));
        }
        else
        {
            finalMessage.text = "";
            NextTurn();
        }
                
    }

    IEnumerator Wait2()
    {
        yield return new WaitForSeconds(Constants.PassTime);
        finalMessage.text = "";
        turn = -turn;

        DrawSelectableTiles();
        
        if (selectableTiles.Count == 0)
        {
            if (turn == Constants.Black)
            {
                passBlack = true;
                if (passBlack && passWhite)
                {
                    finalMessage.text = "End Game!!";
                }
                else
                {
                    finalMessage.text = "Pass!!";
                    StartCoroutine("Wait2");
                }
            }
            else
            {
                passWhite = true;
                if (passBlack && passWhite)
                {
                    finalMessage.text = "End Game!!";
                }
                else
                {
                    finalMessage.text = "Pass!!";
                    StartCoroutine("Wait2");
                }
            }
            

        }

        else
        {
            NextTurn();
        }

    }

       
}
