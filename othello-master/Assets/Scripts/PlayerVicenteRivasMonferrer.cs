using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NodeVicenteRivasMonferrer
{
    // board: Una copia del estado del tablero de juego para este nodo particular.
    // Se utiliza para simular movimientos y evaluar diferentes escenarios.
    public Tile[] board = new Tile[Constants.NumTiles];

    // parent: Referencia al nodo padre en el árbol Minimax. Útil para reconstruir la ruta o para ciertas lógicas de poda.
    public NodeVicenteRivasMonferrer parent;

    // childList: Lista de nodos hijos. Cada hijo representa un posible estado del juego resultante de un movimiento.
    public List<NodeVicenteRivasMonferrer> childList = new List<NodeVicenteRivasMonferrer>();

    // type: Indica si el nodo es de tipo MAX (0) o MIN (1).
    // Un nodo MAX intentará maximizar la utilidad (turno de la IA).
    // Un nodo MIN intentará minimizar la utilidad (turno del oponente).
    public int type;

    // utility: El valor numérico que representa qué tan favorable es el estado del tablero en este nodo para la IA.
    // Calculado por la función heurística o propagado desde los nodos hijos.
    public double utility;

    // alfa: Utilizado en la poda Alfa-Beta. Para un nodo MAX, alfa es el valor mínimo garantizado que la IA puede obtener.
    // Se inicializa a menos infinito.
    public double alfa;

    // beta: Utilizado en la poda Alfa-Beta. Para un nodo MIN, beta es el valor máximo garantizado que el oponente permitirá a la IA.
    // Se inicializa a más infinito.
    public double beta;

    // moveIndex: El índice de la casilla (0-63) que representa el movimiento que llevó desde el nodo padre a este nodo.
    // Es -1 si es el nodo raíz o si no se aplica (por ejemplo, en un nodo de "pase").
    public int moveIndex;

    // captures: Lista de índices de las fichas capturadas por el movimiento que generó este nodo.
    // Mantenido por si es útil para heurísticas más avanzadas o depuración, aunque no se usa activamente en la lógica Minimax actual.
    public List<int> captures = new List<int>();

    // Constructor de la clase Node.
    // Inicializa un nuevo nodo con una copia profunda del estado del tablero proporcionado y su tipo (MAX o MIN).
    // Parámetros:
    //   tiles: El estado del tablero (array de Tile) a copiar para este nodo.
    //   nodeType: El tipo del nodo (NODETYPE_MAX o NODETYPE_MIN).
    public NodeVicenteRivasMonferrer(Tile[] tiles, int nodeType)
    {
        // Realiza una copia profunda del tablero para evitar modificaciones no deseadas
        // en el estado del tablero de nodos ancestros o hermanos.
        for (int i = 0; i < tiles.Length; i++)
        {
            board[i] = new Tile(); // Crea una nueva instancia de Tile.
            board[i].value = tiles[i].value;
            board[i].numTile = tiles[i].numTile;
            board[i].fila = tiles[i].fila;
            board[i].columna = tiles[i].columna;
        }
        type = nodeType; // Asigna el tipo de nodo (MAX o MIN).
        // Inicializa alfa y beta para la poda Alfa-Beta.
        // Para un nodo MAX, alfa empieza en -infinito. Para un nodo MIN, beta empieza en +infinito.
        // Estos valores se actualizan durante el recorrido del árbol si se implementa la poda.
        alfa = double.NegativeInfinity;
        beta = double.PositiveInfinity;
        moveIndex = -1; // Por defecto, el movimiento no está definido hasta que se crea como hijo.
    }
}

// Definiciones para los tipos de nodos Minimax, locales a esta clase ya que no podemos modificar Constants.cs
public class PlayerVicenteRivasMonferrer : MonoBehaviour
{
    // Constantes para definir el tipo de nodo en el algoritmo Minimax.
    private const int NODETYPE_MAX = 0; // Representa un nodo donde la IA (jugador actual) intenta maximizar la puntuación.
    private const int NODETYPE_MIN = 1; // Representa un nodo donde el oponente intenta minimizar la puntuación de la IA.
    // MINIMAX_DEPTH: Define la profundidad máxima de búsqueda en el árbol de Minimax.
    // Un valor mayor implica que la IA "piensa" más movimientos adelante, lo que puede resultar en mejores decisiones,
    // pero también incrementa significativamente el tiempo de cálculo. Un valor entre 2 y 4 es común.
    private const int MINIMAX_DEPTH = 4;

    // turn: El color asignado a esta instancia de Player (IA).
    // Puede ser Constants.Black (1) o Constants.White (-1).
    public int turn;
    private BoardManager boardManager;
    
    // lastMovement: Almacena el índice de la casilla del último movimiento seleccionado por esta IA.
    // Se utiliza para una optimización en GetCapturesForMove, evitando recalcular las capturas
    // si se solicitan para el mismo movimiento que acaba de ser elegido.
    private int lastMovement = -1;
    // expectedCaptures: Lista que almacena las fichas que se espera capturar con el 'lastMovement'.
    // Complementa la optimización de 'lastMovement'.
    private List<int> expectedCaptures = new List<int>();

    void Start()
    {
        boardManager = GameObject.FindGameObjectWithTag("BoardManager").GetComponent<BoardManager>();
        Debug.Log("======= INICIALIZACIÓN IA - COLOR: " + (turn == Constants.Black ? "NEGRO" : "BLANCO") + " - Estrategia: MINIMAX Profundidad " + MINIMAX_DEPTH + " =======");
    }

    // Selecciona la mejor casilla donde mover para la IA utilizando Minimax.
    // Esta función es el punto de entrada para la lógica de decisión de la IA.

    public int SelectTile(Tile[] currentBoardState)
    {
        expectedCaptures.Clear(); // Limpia la lista de capturas esperadas del movimiento anterior.
        Debug.Log("======= MINIMAX SELECCIONANDO JUGADA PARA IA - COLOR: " + (turn == Constants.Black ? "NEGRO" : "BLANCO") + " =======");
        
        // Obtiene todos los movimientos válidos para la IA desde el estado actual del tablero.
        List<int> possibleRootMoves = boardManager.FindSelectableTiles(currentBoardState, turn);
        
        // Si no hay movimientos posibles, la IA no puede jugar y retorna -1.
        if (possibleRootMoves.Count == 0)
        {
            Debug.Log("MINIMAX: No hay movimientos disponibles para la IA.");
            lastMovement = -1; // Resetea el último movimiento.
            return -1;
        }

        // Crea el nodo raíz del árbol Minimax. Este nodo representa el estado actual del juego.
        // Es un nodo de tipo MAX porque la IA (este jugador) busca maximizar su utilidad.
        NodeVicenteRivasMonferrer rootNode = new NodeVicenteRivasMonferrer(currentBoardState, NODETYPE_MAX);
        
        // Inicia la ejecución del algoritmo Minimax de forma recursiva con Poda Alfa-Beta.
        // La función MinimaxRecursive explorará el árbol de juego desde rootNode,
        // hasta la profundidad MINIMAX_DEPTH, y calculará la utilidad de rootNode
        // y, crucialmente, de sus hijos directos. Los hijos directos de la raíz representan
        // los posibles primeros movimientos que la IA puede realizar.
        // Se inicializa alfa a -infinito y beta a +infinito para la raíz.
        MinimaxRecursive(rootNode, 0, turn, double.NegativeInfinity, double.PositiveInfinity);

        int bestMoveFound = -1; // Almacenará el índice del mejor movimiento encontrado.
        // La utilidad de la raíz (rootNode) es MAX, por lo que se busca la utilidad más alta entre sus hijos.
        // Se inicializa bestUtility a un valor muy bajo para asegurar que cualquier utilidad de un hijo sea mayor.
        double bestUtility = double.NegativeInfinity; 

        // Si el nodo raíz no tiene hijos después de ejecutar MinimaxRecursive,
        // podría indicar un error o un estado de juego donde todos los caminos llevan a un fin inmediato.
        // Como medida de contingencia, se selecciona el primer movimiento posible si existe.
        if (rootNode.childList.Count == 0)
        {
            Debug.LogWarning("MINIMAX: El nodo raíz no tiene hijos después de la ejecución de Minimax. Usando el primer movimiento posible como fallback.");
            if (possibleRootMoves.Count > 0)
                 bestMoveFound = possibleRootMoves[0];
            else
            return -1; // Si realmente no había movimientos (ya cubierto arriba, pero doble chequeo).
        }
        else
        {
            // Itera sobre cada nodo hijo del nodo raíz. Cada hijo representa un posible primer movimiento de la IA.
            // El objetivo es encontrar el hijo (y por lo tanto, el movimiento) que tenga la mayor utilidad.
            foreach (NodeVicenteRivasMonferrer child in rootNode.childList)
            {
                // La utilidad de 'child' (child.utility) fue calculada por la llamada recursiva de Minimax.
                // Estos nodos hijos de la raíz son nodos MIN (si depth > 0 en la llamada recursiva que los creó),
                // y su utilidad refleja el mejor resultado que el oponente intentaría forzar.
                // El nodo MAX (rootNode) elige el movimiento (child.moveIndex) que conduce al hijo
                // que resulta en la situación más favorable para la IA (mayor utilidad).
                if (child.utility > bestUtility)
                {
                    bestUtility = child.utility;
                    bestMoveFound = child.moveIndex; // child.moveIndex almacena el movimiento que llevó a este nodo hijo.
                }
            }
            // Comprobación de consistencia: la utilidad calculada para el nodo raíz (rootNode.utility)
            // debería ser igual a la mejor utilidad encontrada entre sus hijos (bestUtility).
            // Pequeñas diferencias pueden ser aceptables debido a la precisión de double.
            if (System.Math.Abs(rootNode.utility - bestUtility) > 0.0001 && rootNode.childList.Count > 0) {
                Debug.LogWarningFormat("MINIMAX: Discrepancia en utilidad. Root utility: {0}, Best child utility: {1}", rootNode.utility, bestUtility);
            }
        }
        
        // Si, por alguna razón excepcional (ej. error lógico), no se encontró un 'bestMoveFound'
        // pero existían movimientos posibles inicialmente, se recurre al primer movimiento disponible.
        if (bestMoveFound == -1 && possibleRootMoves.Count > 0) {
            Debug.LogWarning("MINIMAX: No se pudo determinar el mejor movimiento con la lógica principal, usando el primer movimiento posible como fallback.");
            bestMoveFound = possibleRootMoves[0];
        }

        lastMovement = bestMoveFound; // Almacena el movimiento elegido para optimizaciones futuras.
        if (bestMoveFound != -1)
        {
            // Si se encontró un movimiento, calcula y almacena las piezas que se capturarían con este movimiento.
            // Esto es para la optimización en GetCapturesForMove.
            expectedCaptures = boardManager.FindSwappablePieces(currentBoardState, bestMoveFound, turn);
            Debug.Log("MINIMAX MOVIMIENTO IA: Posición " + bestMoveFound + " (" + (bestMoveFound / 8) + "," + (bestMoveFound % 8) + ") con utilidad esperada: " + bestUtility);
            Debug.Log("CAPTURAS ESPERADAS: " + expectedCaptures.Count);
        }
        else
        {
            Debug.Log("MINIMAX: No se seleccionó ningún movimiento (bestMoveFound es -1).");
        }
        
        return bestMoveFound; // Devuelve el índice del mejor movimiento encontrado.
    }

    // Calcula la utilidad de un nodo dado, explorando el árbol de juego hasta una profundidad definida.
    // Incluye la optimización de Poda Alfa-Beta.
    // Parámetros:
    //   node: El nodo actual del árbol de juego que se está evaluando.
    //   depth: La profundidad actual en el árbol de búsqueda.
    //   playerColor: El color del jugador cuyo turno se está simulando.
    //   alfa: El mejor valor (máximo) encontrado hasta ahora para el jugador MAX en la ruta actual.
    //   beta: El mejor valor (mínimo) encontrado hasta ahora para el jugador MIN en la ruta actual.
    // Retorna:
    //   La utilidad calculada para el 'node' dado.
    private double MinimaxRecursive(NodeVicenteRivasMonferrer node, int depth, int playerColor, double alfa, double beta)
    {
        // --- Recorrido del Árbol y Condiciones de Terminación ---
        // Condición de terminación 1: Se ha alcanzado la profundidad máxima de búsqueda (MINIMAX_DEPTH).
        if (depth == MINIMAX_DEPTH)
        {
            node.utility = HeuristicEvaluation(node.board, this.turn); 
            return node.utility;
        }

        // Obtiene todos los movimientos posibles para 'playerColor' en el estado actual del tablero del 'node'.
        List<int> possibleMoves = boardManager.FindSelectableTiles(node.board, playerColor);

        // Condición de terminación 2: Situación de "no hay movimientos".
        // Si 'playerColor' no tiene movimientos válidos:
        if (possibleMoves.Count == 0)
        {
            int opponentColor = -playerColor; // Determina el color del oponente.
            // Se comprueba si el oponente ('opponentColor') TAMPOCO tiene movimientos.
            List<int> opponentMoves = boardManager.FindSelectableTiles(node.board, opponentColor);

            if (opponentMoves.Count == 0) // Fin del juego: Ningún jugador puede mover.
            {
                // Es un estado terminal. Se evalúa el tablero con la heurística.
                node.utility = HeuristicEvaluation(node.board, this.turn);
                return node.utility;
            }
            else // El jugador actual ('playerColor') pasa, pero el oponente ('opponentColor') sí puede mover.
            {
                // Se simula un "pase de turno". El estado del tablero no cambia, pero el turno pasa al oponente.
                // Se crea un nodo hijo que representa esta situación. El tipo de este nodo hijo será el opuesto
                // al del nodo actual ('node.type'), porque el turno cambia de jugador.
                // La profundidad aumenta, y la llamada recursiva se hace para 'opponentColor', pasando alfa y beta.
                NodeVicenteRivasMonferrer childPassNode = new NodeVicenteRivasMonferrer(node.board, (node.type == NODETYPE_MAX ? NODETYPE_MIN : NODETYPE_MAX));
                childPassNode.parent = node;
                node.childList.Add(childPassNode);

                // La utilidad del nodo actual ('node') será la que devuelva la evaluación del estado resultante.
                node.utility = MinimaxRecursive(childPassNode, depth + 1, opponentColor, alfa, beta);
                return node.utility;
            }
        }

        // --- Lógica central de Minimax: Exploración de hijos ---
        // Si no es un nodo terminal y hay movimientos posibles:
        // NOTA para PODA ALFA-BETA:
        // Aquí es donde se integra la lógica de poda Alfa-Beta.
        // Antes de entrar al bucle 'foreach', si este 'node' es MAX, su 'alfa' (heredado o inicial) se compararía
        // con el 'beta' de sus ancestros MIN. Si es MIN, su 'beta' se compararía con el 'alfa' de sus ancestros MAX.
        // Dentro del bucle, después de la llamada recursiva para un 'childNode':
        // - Si 'node' es MAX: node.alfa = Max(node.alfa, childUtility). Si node.alfa >= beta (beta del padre MIN), se poda.
        // - Si 'node' es MIN: node.beta = Min(node.beta, childUtility). Si node.beta <= alfa (alfa del padre MAX), se poda.
        // Los valores alfa y beta del nodo actual ('node') se actualizan y se usan para las podas.

        if (node.type == NODETYPE_MAX) // Es un nodo MAX (turno de la IA o jugador que maximiza).
        {
            node.utility = double.NegativeInfinity; // Inicializa la utilidad a un valor muy bajo.
            foreach (int move in possibleMoves)
            {
                Tile[] nextBoardState = CreateNextBoardState(node.board, move, playerColor);
                NodeVicenteRivasMonferrer childNode = new NodeVicenteRivasMonferrer(nextBoardState, NODETYPE_MIN); 
                childNode.parent = node;
                childNode.moveIndex = move;
                node.childList.Add(childNode);

                // Llama recursivamente a Minimax para el nodo hijo, pasando alfa y beta actuales.
                double childUtility = MinimaxRecursive(childNode, depth + 1, -playerColor, alfa, beta);
                
                if (childUtility > node.utility)
                {
                    node.utility = childUtility;
                }
                // Actualiza alfa para el nodo MAX.
                alfa = System.Math.Max(alfa, node.utility);
                // Condición de Poda Beta: si alfa >= beta, el jugador MIN (ancestro)
                // ya tiene una opción mejor, así que no necesitamos explorar más hijos de este nodo MAX.
                if (alfa >= beta)
                {
                    break; // Poda (Corte Beta)
                }
            }
            return node.utility;
        }
        else // Es un nodo MIN (turno del oponente o jugador que minimiza).
        {
            node.utility = double.PositiveInfinity; // Inicializa la utilidad a un valor muy alto.
            foreach (int move in possibleMoves)
            {
                Tile[] nextBoardState = CreateNextBoardState(node.board, move, playerColor);
                NodeVicenteRivasMonferrer childNode = new NodeVicenteRivasMonferrer(nextBoardState, NODETYPE_MAX); 
                childNode.parent = node;
                childNode.moveIndex = move;
                node.childList.Add(childNode);

                // Llama recursivamente a Minimax para el nodo hijo, pasando alfa y beta actuales.
                double childUtility = MinimaxRecursive(childNode, depth + 1, -playerColor, alfa, beta);
                
                if (childUtility < node.utility)
                {
                    node.utility = childUtility;
                }
                // Actualiza beta para el nodo MIN.
                beta = System.Math.Min(beta, node.utility);
                // Condición de Poda Alfa: si beta <= alfa, el jugador MAX (ancestro)
                // ya tiene una opción mejor, así que no necesitamos explorar más hijos de este nodo MIN.
                if (beta <= alfa)
                {
                    break; // Poda (Corte Alfa)
                }
            }
            return node.utility;
        }
    }
    
    // Crea una copia del estado del tablero y aplica un movimiento sobre esa copia.
    // Necesario porque BoardManager.Move modifica el tablero que se le pasa por referencia.

    private Tile[] CreateNextBoardState(Tile[] currentBoard, int move, int playerColor)
    {
        // Crea un nuevo array para el próximo estado del tablero.
        Tile[] nextBoard = new Tile[Constants.NumTiles];
        // Realiza una copia profunda de cada Tile del tablero actual al nuevo tablero.
        for (int i = 0; i < currentBoard.Length; i++)
        {
            nextBoard[i] = new Tile(); // Es importante crear una nueva instancia de Tile.
            nextBoard[i].value = currentBoard[i].value;
            nextBoard[i].numTile = currentBoard[i].numTile;
            nextBoard[i].fila = currentBoard[i].fila;
            nextBoard[i].columna = currentBoard[i].columna;
        }
        // Aplica el movimiento 'move' realizado por 'playerColor' sobre la copia del tablero 'nextBoard'.
        // La función boardManager.Move modificará 'nextBoard' directamente.
        boardManager.Move(nextBoard, move, playerColor); 
        return nextBoard; // Devuelve el tablero modificado.
    }

    // Función de utilidad heurística para evaluar un estado del tablero.
    // La evaluación se realiza siempre desde la perspectiva de ESTA IA (identificada por 'aiPlayerColor').
    // Una utilidad positiva favorece a la IA, una negativa al oponente.

    private double HeuristicEvaluation(Tile[] board, int aiPlayerColor)
    {
        // Pesos base (pueden considerarse como default o para fases no cubiertas explícitamente si se desea)
        double base_w_pieceDiff = 1.0;
        double base_w_cornerControl = 25.0; // El control de esquinas es consistentemente alto
        double base_w_mobility = 5.0;
        double base_w_xSquares = -12.0;     // Penalización estándar por X-squares
        double base_w_cSquares = -8.0;      // Penalización estándar por C-squares

        // Variables para los pesos que se usarán, inicializadas con los valores base.
        double current_w_pieceDiff = base_w_pieceDiff;
        double current_w_cornerControl = base_w_cornerControl;
        double current_w_mobility = base_w_mobility;
        double current_w_xSquares = base_w_xSquares;
        double current_w_cSquares = base_w_cSquares;

        int aiPieces = 0;
        int opponentPieces = 0;
        int aiFrontierDiscs = 0;
        int opponentFrontierDiscs = 0;

        for (int i = 0; i < Constants.NumTiles; i++)
        {
            if (board[i].value == aiPlayerColor)
            {
                aiPieces++;
                // Comprobar si es una pieza de frontera
                if (IsFrontierDisc(board, i)) aiFrontierDiscs++;
            }
            else if (board[i].value == -aiPlayerColor)
            {
                opponentPieces++;
                if (IsFrontierDisc(board, i)) opponentFrontierDiscs++;
            }
        }

        // 1. Diferencia de Piezas (Piece Difference) - Se calculará después de determinar la fase del juego
        // double pieceDiffScore = current_w_pieceDiff * (aiPieces - opponentPieces); 

        // 2. Control de Esquinas (Corner Control) - Generalmente importante en todas las fases
        double cornerScore = 0;
        int[] corners = {0, 7, 56, 63}; // Índices de las 4 esquinas.
        foreach (int cornerPos in corners) {
            if (board[cornerPos].value == aiPlayerColor) {
                cornerScore += current_w_cornerControl; // Usar peso actual (aunque para esquinas suele ser fijo)
            } else if (board[cornerPos].value == -aiPlayerColor) {
                cornerScore -= current_w_cornerControl;
            }
        }

        // 3. Movilidad (Mobility) - Se calculará después de determinar la fase del juego
        List<int> aiMoves = boardManager.FindSelectableTiles(board, aiPlayerColor);
        List<int> opponentMoves = boardManager.FindSelectableTiles(board, -aiPlayerColor);
        // double mobilityScore = 0; 

        // Determinar la fase del juego y ajustar pesos dinámicamente
        int numTotalPieces = aiPieces + opponentPieces;

        // Fase Temprana (Early Game: < 20-22 piezas en total)
        // Prioridad: movilidad, desarrollo seguro, evitar errores posicionales graves.
        if (numTotalPieces < 22) { 
            current_w_pieceDiff = 0.25;      // Poca importancia a la cuenta de piezas.
            current_w_mobility = 7.5;       // Máxima importancia a la movilidad y opciones.
            current_w_cornerControl = 30.0; // Asegurar/evitar control de esquinas es vital desde el inicio.
            current_w_xSquares = -15.0;     // Muy conservador con X-squares.
            current_w_cSquares = -10.0;     // Muy conservador con C-squares.
        }
        // Fase Media (Mid Game: 22 a 48-50 piezas en total)
        // Agresividad Calculada: Buscar ventajas materiales sin comprometer excesivamente la posición.
        // Empezar a presionar al oponente.
        else if (numTotalPieces < 50) { 
            current_w_pieceDiff = 2.5;      // Aumenta significativamente la búsqueda de ventaja material.
            current_w_mobility = 4.0;       // Movilidad sigue siendo importante, pero se puede sacrificar por buenas ganancias.
            current_w_cornerControl = 25.0; // Las esquinas siguen siendo clave.
            current_w_xSquares = -10.0;     // Aún cauteloso, pero menos que en early game si hay un plan.
            current_w_cSquares = -7.0;
        }
        // Fase Final (Late Game: > 50 piezas en total)
        // Agresividad Total para Maximizar Piezas: La cuenta de piezas es el rey.
        else { 
            current_w_pieceDiff = 10.0;     // ¡Cada pieza cuenta muchísimo! Agresividad máxima en capturas.
            current_w_mobility = 0.5;       // Movilidad casi irrelevante, excepto para permitir la última captura.
            current_w_cornerControl = 15.0; // El valor de esquinas futuras es menor si el juego termina ya.
                                            // Las ya tomadas contribuyen a pieceDiff.
            current_w_xSquares = -2.0;      // Penalizaciones muy bajas, si tomar una X/C-square da más piezas, se hace.
            current_w_cSquares = -1.0;
        }

        // Ahora calcular los scores con los pesos ajustados por la fase del juego:
        double pieceDiffScore = current_w_pieceDiff * (aiPieces - opponentPieces);
        
        double mobilityScore = 0;
        if (aiMoves.Count + opponentMoves.Count != 0) 
        {
            mobilityScore = current_w_mobility * (aiMoves.Count - opponentMoves.Count);
        } else { 
             if (aiPieces > opponentPieces) return 10000 + (aiPieces - opponentPieces); // Ajustado para incluir diferencia
             if (opponentPieces > aiPieces) return -10000 - (opponentPieces - aiPieces);
             return 0; 
        }

        // 4. Penalización por Casillas Peligrosas (X-squares y C-squares)
        double stabilityScore = 0; // Incluye penalizaciones por X/C squares
        
        // Definiciones de X-squares y C-squares relativas a cada esquina
        // Esquina 0 (superior izquierda)
        if (board[0].value == Constants.Empty) {
            if (board[1].value == aiPlayerColor) stabilityScore += current_w_cSquares; // Usar peso actual
            if (board[8].value == aiPlayerColor) stabilityScore += current_w_cSquares; // Usar peso actual
            if (board[9].value == aiPlayerColor) stabilityScore += current_w_xSquares; // Usar peso actual
        }
        // Esquina 7 (superior derecha)
        if (board[7].value == Constants.Empty) {
            if (board[6].value == aiPlayerColor) stabilityScore += current_w_cSquares;
            if (board[15].value == aiPlayerColor) stabilityScore += current_w_cSquares;
            if (board[14].value == aiPlayerColor) stabilityScore += current_w_xSquares;
        }
        // Esquina 56 (inferior izquierda)
        if (board[56].value == Constants.Empty) {
            if (board[48].value == aiPlayerColor) stabilityScore += current_w_cSquares;
            if (board[57].value == aiPlayerColor) stabilityScore += current_w_cSquares;
            if (board[49].value == aiPlayerColor) stabilityScore += current_w_xSquares;
        }
        // Esquina 63 (inferior derecha)
        if (board[63].value == Constants.Empty) {
            if (board[55].value == aiPlayerColor) stabilityScore += current_w_cSquares;
            if (board[62].value == aiPlayerColor) stabilityScore += current_w_cSquares;
            if (board[54].value == aiPlayerColor) stabilityScore += current_w_xSquares;
        }
        
        // (Opcional) 5. Piezas de Frontera (Frontier Discs)
        // Generalmente, es bueno tener menos piezas en la frontera.
        // double frontierScore = -1.0 * (aiFrontierDiscs - opponentFrontierDiscs); // Ejemplo simple
        // Este factor puede ser complejo de balancear y a veces es mejor omitirlo o integrarlo en la movilidad.

        // Combinación de todos los factores
        double totalUtility = pieceDiffScore + cornerScore + mobilityScore + stabilityScore;
        
        // Considerar el final del juego: si el tablero está lleno o un jugador no tiene piezas.
        if (aiPieces + opponentPieces == Constants.NumTiles || aiPieces == 0 || opponentPieces == 0)
        {
            if (aiPieces > opponentPieces) return 10000 + (aiPieces - opponentPieces); // Victoria (añadir diferencia para desempate)
            if (opponentPieces > aiPieces) return -10000 - (opponentPieces - aiPieces); // Derrota
            return 0; // Empate
        }

        return totalUtility; 
    }

    // Nueva función auxiliar para HeuristicEvaluation
    // Verifica si una pieza en una posición dada es una pieza de frontera.
    // Una pieza de frontera es aquella adyacente a al menos una casilla vacía.
    private bool IsFrontierDisc(Tile[] board, int position)
    {
        int r = position / Constants.TilesPerRow;
        int c = position % Constants.TilesPerRow;

        // Direcciones: N, NE, E, SE, S, SW, W, NW
        int[] dr = {-1, -1, 0, 1, 1, 1, 0, -1};
        int[] dc = {0, 1, 1, 1, 0, -1, -1, -1};

        for (int i = 0; i < 8; i++)
        {
            int nr = r + dr[i];
            int nc = c + dc[i];

            if (nr >= 0 && nr < Constants.TilesPerRow && nc >= 0 && nc < Constants.TilesPerRow)
            {
                if (board[nr * Constants.TilesPerRow + nc].value == Constants.Empty)
                {
                    return true; // Adyacente a una casilla vacía
                }
            }
        }
        return false; // No adyacente a ninguna casilla vacía
    }

    // Devuelve la lista de piezas que serían capturadas si se mueve a 'position'.
    // Este método es llamado por Controller.cs y debe funcionar para cualquier jugador.
    // Se optimiza para el último movimiento de la IA si la consulta coincide.
    public List<int> GetCapturesForMove(Tile[] board, int position, int playerColor)
    {
        // Optimización: Si la consulta es para el último movimiento calculado por esta IA
        // y para el mismo color, se devuelven las capturas ya calculadas para evitar retrabajo.
        if (position == lastMovement && playerColor == this.turn && lastMovement != -1)
        {
            // Devuelve una NUEVA lista (copia) para evitar modificaciones externas a 'expectedCaptures'.
            return new List<int>(expectedCaptures); 
        }
        
        // Si no aplica la optimización, calcula las capturas normalmente usando BoardManager.
        // Es crucial usar 'playerColor' (el color del jugador que se está consultando)
        // y no 'this.turn' (el color de esta instancia de IA), para que la función sea general
        // y pueda ser usada por el Controller para cualquier jugador.
        List<int> currentCaptures = boardManager.FindSwappablePieces(board, position, playerColor);
        
        return currentCaptures;
    }

    // Cuenta cuántas fichas de un color hay en el tablero.
    public int CountPieces(Tile[] board, int playerColor)
    {
        return boardManager.CountPieces(board, playerColor);
    }
}