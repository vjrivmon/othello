using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Necesario para Max/Min en algunas implementaciones, aunque lo haremos manualmente

/// <summary>
/// Clase auxiliar para el Player. 
/// Helper class for Player.
/// </summary>
public class Node
{
    public Tile[] board = new Tile[Constants.NumTiles];
    public Node parent;
    public List<Node> childList = new List<Node>();
    public int type; // 0 para MAX, 1 para MIN (según nuestras constantes en Player)
    public double utility;
    public double alfa;
    public double beta;
    public int moveIndex; // El movimiento que llevó a este estado de nodo desde su padre
    public List<int> captures = new List<int>(); // Originalmente en la clase Node, mantenido por si acaso

    public Node(Tile[] tiles, int nodeType)
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            // Asegurarse de que cada Tile en el nuevo tablero del nodo es una nueva instancia
            board[i] = new Tile();
            board[i].value = tiles[i].value;
            board[i].numTile = tiles[i].numTile;
            board[i].fila = tiles[i].fila;
            board[i].columna = tiles[i].columna;
        }
        type = nodeType;
        alfa = double.NegativeInfinity;
        beta = double.PositiveInfinity;
        moveIndex = -1; // Por defecto; se establece explícitamente cuando se crea como hijo
        // captures = new List<int>(); // Inicializado arriba
    }
}

// Definiciones para los tipos de nodos Minimax, locales a esta clase ya que no podemos modificar Constants.cs
public class Player : MonoBehaviour
{
    private const int NODETYPE_MAX = 0;
    private const int NODETYPE_MIN = 1;
    // Profundidad de búsqueda Minimax (entre 2 y 4 según la práctica)
    // A mayor profundidad, mejor jugará la IA pero más tiempo tardará en calcular.
    private const int MINIMAX_DEPTH = 2; 

    public int turn;  // Color de este Player (1=negro, -1=blanco)
    private BoardManager boardManager;
    
    // Almacena el último movimiento realizado por la IA para optimizar GetCapturesForMove
    private int lastMovement = -1;
    // Almacena las capturas esperadas para el último movimiento de la IA
    private List<int> expectedCaptures = new List<int>();

    void Start()
    {
        boardManager = GameObject.FindGameObjectWithTag("BoardManager").GetComponent<BoardManager>();
        Debug.Log("======= INICIALIZACIÓN IA - COLOR: " + (turn == Constants.Black ? "NEGRO" : "BLANCO") + " - Estrategia: MINIMAX Profundidad " + MINIMAX_DEPTH + " =======");
    }

    /// <summary>
    /// Selecciona la mejor casilla donde mover para la IA utilizando Minimax.
    /// Esta función es el punto de entrada para la lógica de decisión de la IA.
    /// </summary>
    /// <param name="currentBoardState">El estado actual del tablero.</param>
    /// <returns>El índice de la casilla donde la IA decide mover, o -1 si no hay movimientos.</returns>
    public int SelectTile(Tile[] currentBoardState)
    {
        expectedCaptures.Clear(); // Limpiar capturas de movimientos anteriores
        Debug.Log("======= MINIMAX SELECCIONANDO JUGADA PARA IA - COLOR: " + (turn == Constants.Black ? "NEGRO" : "BLANCO") + " =======");
        
        // Obtener los movimientos posibles desde el estado actual para la IA
        List<int> possibleRootMoves = boardManager.FindSelectableTiles(currentBoardState, turn);
        
        if (possibleRootMoves.Count == 0)
        {
            Debug.Log("MINIMAX: No hay movimientos disponibles para la IA.");
            lastMovement = -1;
            return -1; // No hay movimientos posibles
        }

        // Nodo raíz del árbol Minimax. Es un nodo MAX porque la IA (este player) quiere maximizar su utilidad.
        Node rootNode = new Node(currentBoardState, NODETYPE_MAX);
        
        // La llamada a MinimaxRecursive calculará la utilidad para rootNode
        // y, crucialmente, para sus hijos directos, que representan los primeros movimientos de la IA.
        MinimaxRecursive(rootNode, 0, turn);

        int bestMoveFound = -1;
        // La raíz es MAX, busca la utilidad más alta entre sus hijos.
        double bestUtility = double.NegativeInfinity; 

        if (rootNode.childList.Count == 0)
        {
            // Esto podría ocurrir si todos los caminos llevan a un fin de juego inmediato sin movimientos válidos,
            // o si hay un error en la generación de hijos. Como fallback, se toma el primer movimiento posible.
            Debug.LogWarning("MINIMAX: El nodo raíz no tiene hijos después de la ejecución de Minimax. Usando el primer movimiento posible como fallback.");
            if (possibleRootMoves.Count > 0) // Asegurarse de que realmente hay movimientos
                 bestMoveFound = possibleRootMoves[0];
            else // Si no hay, aunque ya se comprobó antes, es un estado de no movimiento.
            return -1;
        }
        else
        {
            // Iterar sobre los hijos del nodo raíz. Cada hijo representa un posible primer movimiento de la IA.
            // Se elige el movimiento (moveIndex del hijo) que lleva al hijo con la utilidad MÁXIMA.
            foreach (Node child in rootNode.childList)
            {
                // child.utility fue calculado por la llamada recursiva de Minimax.
                // Para un hijo de la raíz (que sería un nodo MIN si la profundidad es >0), 
                // su utilidad representa el valor que el oponente trataría de imponer.
                // El nodo MAX (root) elige el movimiento que lleva al hijo que le es más favorable (mayor utilidad).
                if (child.utility > bestUtility)
                {
                    bestUtility = child.utility;
                    // child.moveIndex guarda el movimiento que llevó a este estado/nodo hijo
                    bestMoveFound = child.moveIndex; 
                }
            }
             // Comprobación de consistencia: la utilidad del nodo raíz debería ser igual a bestUtility.
            if (System.Math.Abs(rootNode.utility - bestUtility) > 0.0001 && rootNode.childList.Count > 0) {
                Debug.LogWarningFormat("MINIMAX: Discrepancia en utilidad. Root utility: {0}, Best child utility: {1}", rootNode.utility, bestUtility);
            }
        }
        
        // Si, por alguna razón extrema, no se encontró un bestMove pero había movimientos iniciales.
        if (bestMoveFound == -1 && possibleRootMoves.Count > 0) {
            Debug.LogWarning("MINIMAX: No se pudo determinar el mejor movimiento con la lógica principal, usando el primer movimiento posible como fallback.");
            bestMoveFound = possibleRootMoves[0];
        }

        lastMovement = bestMoveFound; // Guardar el movimiento elegido
        if (bestMoveFound != -1)
        {
            // Calcular y guardar las capturas para este movimiento, para optimizar GetCapturesForMove
            expectedCaptures = boardManager.FindSwappablePieces(currentBoardState, bestMoveFound, turn);
            Debug.Log("MINIMAX MOVIMIENTO IA: Posición " + bestMoveFound + " (" + (bestMoveFound / 8) + "," + (bestMoveFound % 8) + ") con utilidad esperada: " + bestUtility);
            Debug.Log("CAPTURAS ESPERADAS: " + expectedCaptures.Count);
        }
        else
        {
            Debug.Log("MINIMAX: No se seleccionó ningún movimiento.");
        }
        
        return bestMoveFound;
    }

    /// <summary>
    /// Función recursiva principal del algoritmo Minimax.
    /// Calcula la utilidad de un nodo dado, explorando el árbol de juego hasta una profundidad definida.
    /// </summary>
    /// <param name="node">El nodo actual en el árbol de juego a evaluar.</param>
    /// <param name="depth">La profundidad actual en el árbol (0 para el nodo raíz).</param>
    /// <param name="playerColor">El color del jugador cuyo turno es en ESTE nodo.</param>
    /// <returns>La utilidad calculada para el 'node' dado.</returns>
    private double MinimaxRecursive(Node node, int depth, int playerColor)
    {
        // Condición terminal: se ha alcanzado la profundidad máxima de búsqueda.
        // O, si el juego ha terminado (ningún jugador puede mover, manejado abajo).
        if (depth == MINIMAX_DEPTH)
        {
            // Se evalúa el tablero desde la perspectiva de la IA (this.turn).
            node.utility = HeuristicEvaluation(node.board, this.turn); 
            return node.utility;
        }

        // Obtener los movimientos posibles para el jugador actual en el estado del tablero del nodo.
        List<int> possibleMoves = boardManager.FindSelectableTiles(node.board, playerColor);

        if (possibleMoves.Count == 0) // El jugador 'playerColor' no tiene movimientos.
        {
            int opponentColor = -playerColor;
            // Comprobar si el oponente tampoco tiene movimientos (fin del juego).
            List<int> opponentMoves = boardManager.FindSelectableTiles(node.board, opponentColor);

            if (opponentMoves.Count == 0) // Fin del juego: ningún jugador puede mover.
            {
                // Evaluar el estado final.
                node.utility = HeuristicEvaluation(node.board, this.turn);
                return node.utility;
            }
            else
            {
                // El jugador actual (playerColor) pasa turno. El oponente (opponentColor) juega.
                // Se genera un nodo hijo para representar el turno del oponente en el MISMO estado de tablero.
                // El tipo de nodo hijo es el opuesto al actual, ya que el turno cambia de jugador.
                Node childPassNode = new Node(node.board, (node.type == NODETYPE_MAX ? NODETYPE_MIN : NODETYPE_MAX));
                childPassNode.parent = node;
                node.childList.Add(childPassNode);

                // La utilidad del nodo actual (donde se pasó turno) es la que devuelva el Minimax del hijo.
                // La profundidad se incrementa, y el turno es del oponente.
                node.utility = MinimaxRecursive(childPassNode, depth + 1, opponentColor);
                return node.utility;
            }
        }

        if (node.type == NODETYPE_MAX) // Nodo MAX (corresponde a un turno de la IA)
        {
            node.utility = double.NegativeInfinity; // Inicializar para encontrar el máximo.
            foreach (int move in possibleMoves)
            {
                // Crear el estado del tablero resultante de aplicar 'move'.
                Tile[] nextBoardState = CreateNextBoardState(node.board, move, playerColor);
                // Crear nodo hijo. Será un nodo MIN, ya que simula el turno del oponente.
                Node childNode = new Node(nextBoardState, NODETYPE_MIN); 
                childNode.parent = node;
                childNode.moveIndex = move; // Guardar el movimiento que llevó a este estado hijo.
                node.childList.Add(childNode);

                // Llamada recursiva para el hijo. La profundidad aumenta, y el turno es del oponente (-playerColor).
                double childUtility = MinimaxRecursive(childNode, depth + 1, -playerColor); 
                // El nodo MAX elige la utilidad máxima de sus hijos.
                if (childUtility > node.utility)
                {
                    node.utility = childUtility;
                }
            }
            return node.utility;
        }
        else // Nodo MIN (corresponde a un turno del oponente)
        {
            node.utility = double.PositiveInfinity; // Inicializar para encontrar el mínimo.
            foreach (int move in possibleMoves)
            {
                Tile[] nextBoardState = CreateNextBoardState(node.board, move, playerColor);
                // Crear nodo hijo. Será un nodo MAX.
                Node childNode = new Node(nextBoardState, NODETYPE_MAX); 
                childNode.parent = node;
                childNode.moveIndex = move;
                node.childList.Add(childNode);

                double childUtility = MinimaxRecursive(childNode, depth + 1, -playerColor);
                // El nodo MIN elige la utilidad mínima de sus hijos (peor caso para la IA).
                if (childUtility < node.utility)
                {
                    node.utility = childUtility;
                }
            }
            return node.utility;
        }
    }
    
    /// <summary>
    /// Crea una copia del estado del tablero y aplica un movimiento sobre esa copia.
    /// Necesario porque BoardManager.Move modifica el tablero que se le pasa por referencia.
    /// </summary>
    /// <param name="currentBoard">El tablero base sobre el que se simulará el movimiento.</param>
    /// <param name="move">La posición donde se realizará el movimiento.</param>
    /// <param name="playerColor">El color del jugador que realiza el movimiento.</param>
    /// <returns>Un NUEVO array Tile[] representando el tablero después del movimiento.</returns>
    private Tile[] CreateNextBoardState(Tile[] currentBoard, int move, int playerColor)
    {
        Tile[] nextBoard = new Tile[Constants.NumTiles];
        for (int i = 0; i < currentBoard.Length; i++)
        {
            nextBoard[i] = new Tile(); // Crear nueva instancia de Tile para la copia.
            nextBoard[i].value = currentBoard[i].value;
            nextBoard[i].numTile = currentBoard[i].numTile;
            nextBoard[i].fila = currentBoard[i].fila;
            nextBoard[i].columna = currentBoard[i].columna;
        }
        // Aplicar el movimiento sobre la copia del tablero.
        boardManager.Move(nextBoard, move, playerColor); 
        return nextBoard;
    }

    /// <summary>
    /// Función de utilidad heurística para evaluar un estado del tablero.
    /// La evaluación se realiza siempre desde la perspectiva de ESTA IA (identificada por 'aiPlayerColor').
    /// Una utilidad positiva favorece a la IA, una negativa al oponente.
    /// </summary>
    /// <param name="board">El estado del tablero a evaluar.</param>
    /// <param name="aiPlayerColor">El color de la IA que está evaluando (this.turn).</param>
    /// <returns>Un valor double representando la "bondad" del tablero para la IA.</returns>
    private double HeuristicEvaluation(Tile[] board, int aiPlayerColor)
    {
        int aiScore = boardManager.CountPieces(board, aiPlayerColor);
        int opponentScore = boardManager.CountPieces(board, -aiPlayerColor);
        
        double utility = aiScore - opponentScore; // Heurística básica: diferencia de piezas.

        // Mejora: Bonificación por control de esquinas.
        // Las esquinas son posiciones estratégicamente valiosas en Othello.
        int[] corners = {0, 7, 56, 63}; // Índices de las 4 esquinas.
        double cornerBonus = 10.0;      // Puntuación adicional por cada esquina controlada. Ajustar según sea necesario.

        foreach (int cornerPos in corners) {
            if (board[cornerPos].value == aiPlayerColor) {
                utility += cornerBonus;
            } else if (board[cornerPos].value == -aiPlayerColor) {
                utility -= cornerBonus;
            }
        }
        // Otras posibles mejoras a la heurística (no implementadas aquí para mantenerlo simple inicialmente):
        // - Movilidad: Número de movimientos disponibles para la IA vs. el oponente.
        // - Estabilidad de las piezas: Piezas que no pueden ser flanqueadas por el oponente.
        // - Control de bordes.
        // - Patrones específicos.
        return utility;
    }

    /// <summary>
    /// Devuelve la lista de piezas que serían capturadas si se mueve a 'position'.
    /// Este método es llamado por Controller.cs y debe funcionar para cualquier jugador.
    /// Se optimiza para el último movimiento de la IA si la consulta coincide.
    /// </summary>
    public List<int> GetCapturesForMove(Tile[] board, int position, int playerColor)
    {
        // Debug.Log("======= GetCapturesForMove LLAMADO - Posición: " + position + " =======");
        // Debug.Log("Color solicitado: " + (playerColor == Constants.Black ? "NEGRO" : "BLANCO"));
        // Debug.Log("Nuestro color (IA): " + (this.turn == Constants.Black ? "NEGRO" : "BLANCO"));

        // Optimización: Si la consulta es para el último movimiento realizado por esta IA
        // y para el color de esta IA, devolver las capturas ya calculadas.
        if (position == lastMovement && playerColor == this.turn && lastMovement != -1)
        {
            // Debug.Log("COINCIDE CON ÚLTIMO MOVIMIENTO DE IA - Devolviendo capturas guardadas: " + expectedCaptures.Count);
            return new List<int>(expectedCaptures); // Devolver una copia para evitar modificaciones externas.
        }
        
        // Si no es el caso anterior, calcular las capturas.
        // Es crucial usar 'playerColor' (el color del jugador que realiza el movimiento) 
        // y no 'this.turn' (el color de la IA) para que la función sea general.
        // Debug.Log("CALCULANDO NUEVAS CAPTURAS para posición " + position + " con color " + (playerColor == Constants.Black ? "NEGRO" : "BLANCO"));
        List<int> currentCaptures = boardManager.FindSwappablePieces(board, position, playerColor);
        
        // Debug.Log("NUEVAS CAPTURAS: " + currentCaptures.Count);
        return currentCaptures;
    }

    /// <summary>
    /// Cuenta cuántas fichas de un color hay en el tablero.
    /// Delegado a BoardManager, pero mantenido aquí si Player lo necesita directamente o para seguir la estructura.
    /// </summary>
    public int CountPieces(Tile[] board, int playerColor)
    {
        return boardManager.CountPieces(board, playerColor);
    }
}