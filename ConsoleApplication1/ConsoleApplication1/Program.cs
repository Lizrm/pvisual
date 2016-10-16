using System;
using System.Threading;
using System.IO;


/*****/
//Contextos
using System.Threading.Tasks;
using System.Collections;

namespace MultiThread
{
    class Procesador
    {

        public static int[] memDatos;             // Cache de datos (cada nucleo tiene una propia)
        public static int[] memInstruc;           // Cache de instrucciones (cada nucleo tiene una propia)
        public static Contextos cola;             // Cola para poder cambiar de contexto entre hilillos, contiene PC, regitsros y contador de ciclos de reloj
        public static Contextos finalizados;      // Guarda el estado de los registros y las cache en la que termino el hilillo
        public static int total;                  // Total de hilillos
        public static int reloj;                  // Variable general del reloj
        public static int quantumTotal;           // Variable compartida, solo de lectura, no debe ser modificada por los hilos
        public static int[,] cacheDatos3;         // 4 columnas 6 filas, (fila 0 p0, fila 2 p1, fila 4 etiqueta, fila 5 valides)
        public static int[,] cacheDatos2;         // 4 columnas 6 filas, (fila 0 p0, fila 2 p1, fila 4 etiqueta, fila 5 valides)
        public static int[,] cacheDatos1;         // 4 columnas 6 filas, (fila 0 p0, fila 2 p1, fila 4 etiqueta, fila 5 valides)
        public static int[] RL1;                  // Registros RL (globales)
        public static int[] RL2;
        public static int[] RL3;
        public static int[] busD;                 // Bus de datos
        public static int[] busI;                 // Bus de instrucciones
        public static int llegan = 4;             // Variable para el uso de la barrera

        static Barrier barrier = new Barrier(llegan, (bar) =>  //Barrera de sincronizacion, lo que esta dentro se ejecuta una sola vez
        {
            reloj++;
           // Console.WriteLine("Tic de reloj");
        }); //FIN de la Barrera

        static public void TicReloj()
        {
            barrier.SignalAndWait();
        }//FIN de TicReloj

        static public void FallodeCache(int ciclos)     // Se encicla los tick de reloj dependiendo que la instruccion que se esta ejecutando
        {
            for (int i = 0; i < ciclos; ++i)            // Simulacion de que un fallo de cache
            {
                TicReloj();                             // Tick de reloj
            }
        }//FIN de Fallo de Cache

        static void Main()
        {
            //**Bloque de creacion**//

            memDatos = new int[96];         // 384/4 //Cambiar por 96 despues
            memInstruc = new int[640];      // 40 bloques * 4 *4
            cola = new Contextos();
            finalizados = new Contextos();

            RL1 = new int[1];               // Inicializacion de resgistros RL
            RL2 = new int[1];
            RL3 = new int[1];
            busD = new int[1];              // Inicializacion del Bus de Datos
            busI = new int[1];              // Inicializacion del Bus de Instrucciones

            cacheDatos1 = new int[6, 4]; //Preguntar si es recomendable recorrerlas por filas
            cacheDatos2 = new int[6, 4];
            cacheDatos3 = new int[6, 4];
            //*******************Fin de Bloque***********************//

            //*************Bloque de inicializacion******************//
            reloj = 0;
            for (int i = 0; i < 96; ++i)    // Memoria principal inicilizada en uno
            {
                memDatos[i] = 1;        
                memInstruc[i] = 1;
            }
            for (int i = 96; i < 640; ++i)   // Memoria principal inicilizada en uno
            {
                memInstruc[i] = 1;
            }

            for (int i = 0; i < 4; ++i)     // Las caches se inicializadas en cero
            {
                for (int j = 0; j < 4; ++j)
                {
                    cacheDatos3[i, j] = 0;
                    cacheDatos2[i, j] = 0;
                    cacheDatos1[i, j] = 0;
                }
            }
            for (int i = 4; i < 6; ++i)     // Las caches se inicializadas en invalidas
            {
                for (int j = 0; j < 4; ++j)
                {
                    cacheDatos3[i, j] = -1;
                    cacheDatos2[i, j] = -1;
                    cacheDatos1[i, j] = -1;
                }
            }
            //*****************Fin de Bloque*************************//

            Console.Write("Ingrese el quantum \n");
            quantumTotal = int.Parse(Console.ReadLine());
            Console.Write("\nIngrese el numero de hilillos Totales \n");
            total = int.Parse(Console.ReadLine());

            /*****Leer de archivos*******/
            int index = 0;
            Char delimiter = ' ';

            for (int i = 0; i < total; ++i) 
            {
                cola.Encolar(index);
                string[] lines = File.ReadAllLines(0 +".txt");   //Funciona si los archivos estan en bin, hay que cambiarlo

                foreach(string line in lines)
                {
                    string[] substrings = line.Split(delimiter);    //Se recortan los espacios en blanco

                    foreach(var subestring in substrings)
                    {
                        memInstruc[index] = int.Parse(subestring);                   
                        ++index;
                    }
                }                
            }
            /******Fin leer de archivos*******/

            //Creacion de los 3 hilos que emulan los nucleos
             Thread thread1 = new Thread(() => Nucleos(quantumTotal));
             Thread thread2 = new Thread(() => Nucleos(quantumTotal));
             Thread thread3 = new Thread(() => Nucleos(quantumTotal));
             //Se les asigna un "id" a los hilos
             thread1.Name = "1";
             thread2.Name = "2";
             thread3.Name = "3";
             //Se inician los hilos
             thread1.Start();
             thread2.Start();
             thread3.Start();


             //Verificar que todos los hilillos finalizaron
             int cardinalidad;
             cardinalidad = 0;
             while (cardinalidad < total)
             {
                 if (!Monitor.TryEnter(finalizados))
                 {
                     TicReloj();
                 }
                 else
                 {
                     TicReloj();
                     cardinalidad = finalizados.Cantidad();
                     Monitor.Exit(finalizados);
                 }               
             }

             //Finaliza los 3 hilos que emulan los nucleos               //Preguntar, depues de matar los hilos, debo segui dando tic de reloj??
             thread1.Abort();
             thread2.Abort();
             thread3.Abort();

             finalizados.Imprimir();

        }//FIN de Main

        public static void Nucleos(int q) //quatum
        {
            /**Bloque de Declaracion**/
            int[] reg;               //Vector de registros
            int[][] cacheInstruc = new int[6][];     //16 columnas 6 filas, (fila 0 p0, fila 2 p1, fila 4 etiqueta, fila 5 valides)
            int PC;                   //Para el control de las instrucciones
            int cop, rf1, rf2, rd;//codigo de operacion, registro fuente, registro fuente2 o registro destino dependiendo de la instruccion, rd registro destino o inmediato dependiendo de la instruccion
            int bloque, posicion, palabra, iterador, quantum, inicioBloque; // bloque es el bloque de memoria cache, quatum es el tiempo dado por el usuario
            int cpu;    //Tic de reloj que dura un hilillo en ejecucion
            int inicioReloj;
            /**Bloque de Creacion**/
            reg = new int[32];
            cacheInstruc[0] = new int[16];
            cacheInstruc[1] = new int[16];
            cacheInstruc[2] = new int[16];
            cacheInstruc[3] = new int[16];
            cacheInstruc[4] = new int[4];     //Para no desperdiciar memoria
            cacheInstruc[5] = new int[4];
            //*****************Fin bloque de creacion******************//

            //***********Bloque inicializacion***********************//

            for (int i = 0; i < 32; ++i)
            {
                reg[i] = 0; //Debo incializarlo en 0
            }

            for (int i = 0; i < 4; ++i) //las caches se inicializadas en cero
            {
                for (int j = 0; j < 16; ++j)
                {
                    cacheInstruc[i][j] = 0;
                }
            }
            for (int i = 4; i < 6; ++i) //las caches se inicializadas en -1
            {
                for (int j = 0; j < 4; ++j)
                {
                    cacheInstruc[i][j] = -1;
                }
            }
            //**************Fin bloque inicilaizacion****************//
            Console.WriteLine("entra al hilo");
            while (true)//while que no deja que los hilos mueran
            {
                bool vacia = true;
                while(vacia)
                {
                    while (!Monitor.TryEnter(cola))
                    {
                        TicReloj();
                    }
                    TicReloj();
                    if (cola.Cantidad() > 0)
                    {
                        vacia = false;
                    }
                    else
                    {
                        Monitor.Exit(cola);
                    }
                    
                }
                
                Console.WriteLine("Consigue la cola");
                switch (int.Parse(Thread.CurrentThread.Name)) //RL
                {
                    case 1:
                        while (!Monitor.TryEnter(RL1))
                        {
                            TicReloj();
                        }
                        TicReloj();
                        RL1[0] = -1;
                        Monitor.Exit(RL1);
                        break;

                    case 2:
                        while (!Monitor.TryEnter(RL2))
                        {
                            TicReloj();
                        }
                        TicReloj();
                        RL2[0] = -1;
                        Monitor.Exit(RL2);
                        break;

                    case 3:
                        while (!Monitor.TryEnter(RL3))
                        {
                            TicReloj();
                        }
                        TicReloj();
                        RL3[0] = -1;
                        Monitor.Exit(RL3);
                        break;
                }
                
                cpu = 0;
                inicioReloj = reloj;
                cola.Sacar(out PC, ref reg, ref cpu);

                Monitor.Exit(cola);
                quantum = q;

                while (quantum > 0)
                {
                 
                    /**************************/
                    bloque = PC / 16;  //calculo el bloque
                    posicion = bloque % 4;    //posicion en cache
                    palabra = (PC % 16) / 4;
                    /*************************/
                    if (!(cacheInstruc[4][posicion] == bloque) && !(cacheInstruc[4][posicion] == 1)) //1 valido
                    {
                        // fallo de cache
                        while (!Monitor.TryEnter(busI))
                        {
                            TicReloj();
                        }
                        TicReloj();

                        cacheInstruc[4][posicion] = bloque;
                        cacheInstruc[5][posicion] = 1;

                        
                        inicioBloque = (bloque * 16);// - 384;// bloque de instrucciones
                        for (int i = 0; i < 4; ++i)
                        {
                            iterador = posicion * 4;
                            for (int j = 0; j < 4; ++j)
                            {
                                cacheInstruc[i][iterador] = memInstruc[inicioBloque];                              
                                ++inicioBloque;
                                ++iterador;                       
                            }
                        }
                        FallodeCache(28);
                        Monitor.Exit(busI);
                    } //Fin fallo de cache

                    iterador = posicion * 4;
                    cop = cacheInstruc[palabra][iterador];
                    rf1 = cacheInstruc[palabra][iterador + 1];
                    rf2 = cacheInstruc[palabra][iterador + 2];
                    rd = cacheInstruc[palabra][iterador + 3];  //destino
                    PC += 4;

                    //Codificacion de las instrucciones recibidas
                    switch (cop) //cop es el codigo de operacion 		// se deben verificar que el registro destino no sea cero 
                    {

                        case 0:
                            Console.WriteLine("LEE ceros");
                            break;
                        case 8: //DADDI rf1 <------- rf2 + inm

                            Console.WriteLine("Hace caso 8: DADDI");
                            reg[rf2] = reg[rf1] + rd;
                            break;

                        case 32: //DADD rd <------ rf1 + rf2

                            Console.WriteLine("Hace caso 32: DADD");
                            reg[rd] = reg[rf1] + reg[rf2];
                            break;

                        case 34: //DSUB  rd <------- rf1 - rf2

                            Console.WriteLine("Hace caso 34: DSUB");
                            reg[rd] = reg[rf1] - reg[rf2];
                            break;

                        case 12: //DMUL  rd <------ rf1 * rf2

                            Console.WriteLine("Hace caso 12: DMUL");
                            reg[rd] = reg[rf1] * reg[rf2];
                            break;

                        case 14: //DIV  rd <------ rf1 / rf2

                            Console.WriteLine("Hace caso 14: DIV");
                            reg[rd] = reg[rf1] / reg[rf2];
                            break;

                        case 4: //BEZ si rf = 0 entonces SALTA

                            Console.WriteLine("Hace caso 4: BEZ");
                            if (reg[rf1] == reg[0])
                            {
                                PC += (rd * 4);
                            }
                            break;

                        case 5: //BNEZ si rf z 0 o rf > 0 entonces SALTA

                            Console.WriteLine("Hace caso 5: BNEZ");
                            if (reg[rf1] != 0)
                            {
                                PC += (rd * 4);
                            }
                            break;

                        case 3: //JAL  reg 31 = PC

                            Console.WriteLine("Hace caso 3: JAL");
                            reg[31] = PC;
                            PC += rd;

                            break;

                        case 2:  //JR  PC = rf1

                            Console.WriteLine("Hace caso 2: JR");
                            PC = reg[rf1];
                            break;

                        case 50: //LL
                                 //Se implementará en la tercera entrega
                            break;

                        case 51: //SC
                                 //Se implementará en la tercera entrega
                            break;

                        case 35: //LW

                            Console.WriteLine("Hace caso 35: LW");
                            int dir = reg[rf1] + rd;
                            bloque = dir / 16;
                            posicion = bloque % 4;
                            palabra = (dir % 16) / 4;     //caculo de bloque y palabra
                            switch (int.Parse(Thread.CurrentThread.Name))
                            {
                                case 1:
                                    bool conseguido = false;
                                    while (!conseguido)
                                    {
                                        while (!Monitor.TryEnter(cacheDatos1))    //cambiar por mi cache
                                        {
                                            TicReloj();
                                        }
                                        TicReloj();
                                        if (!(bloque == cacheDatos1[4, posicion]) && !(cacheDatos1[5, posicion] == 1))
                                        {
                                            if (!Monitor.TryEnter(busD))
                                            {
                                                Monitor.Exit(cacheDatos1); //cambiar por mi cache de datos
                                                TicReloj();
                                            }
                                            else
                                            {
                                                TicReloj();
                                                inicioBloque = bloque * 4;    //inicio del bloque a copiar

                                                for (int i = 0; i < 4; ++i) //Copia los datos de memoria a Cache
                                                {
                                                    cacheDatos1[i, posicion] = memDatos[inicioBloque];
                                                    inicioBloque++;
                                                }
                                                cacheDatos1[4, posicion] = bloque;
                                                cacheDatos1[5, posicion] = 1;
                                                FallodeCache(28);
                                                Monitor.Exit(busD);
                                            }
                                        }
                                        else
                                        {
                                            conseguido = true;
                                        }
                                    }
                                    reg[rf2] = cacheDatos1[palabra, posicion];
                                    Monitor.Exit(cacheDatos1);

                                    break;

                                case 2:
                                    bool c2 = false;
                                    while (!c2)
                                    {
                                        while (!Monitor.TryEnter(cacheDatos2))    //cambiar por mi cache
                                        {
                                            TicReloj();
                                        }
                                        TicReloj();
                                        if (!(bloque == cacheDatos2[4, posicion]) && !(cacheDatos2[5, posicion] == 1))
                                        {
                                            if (!Monitor.TryEnter(busD))
                                            {
                                                Monitor.Exit(cacheDatos2); //cambiar por mi cache de datos
                                                TicReloj();
                                            }
                                            else
                                            {
                                                c2 = true;
                                                TicReloj();
                                                inicioBloque = bloque * 4;    //inicio del bloque a copiar

                                                for (int i = 0; i < 4; ++i) //Copia los datos de memoria a Cache
                                                {
                                                    cacheDatos2[i, posicion] = memDatos[inicioBloque];
                                                    inicioBloque++;
                                                }
                                                cacheDatos2[4, posicion] = bloque;
                                                cacheDatos2[5, posicion] = 1;
                                                FallodeCache(28);
                                                Monitor.Exit(busD);
                                            }
                                        }
                                        else
                                        {
                                            conseguido = true;
                                        }
                                    }                                   
                                    reg[rf2] = cacheDatos1[palabra, posicion];//Se le entrega el dato al registro
                                    Monitor.Exit(cacheDatos2); //soltar mi cache 
                                                                
                                    break;

                                case 3:
                                    bool c3 = false;
                                    while (!c3)
                                    {
                                        while (!Monitor.TryEnter(cacheDatos3))
                                        {
                                            TicReloj();
                                        }
                                        TicReloj();
                                        if (!(bloque == cacheDatos3[4, posicion]) && !(cacheDatos3[5, posicion] == 1))
                                        {
                                            if (!Monitor.TryEnter(busD))
                                            {
                                                Monitor.Exit(cacheDatos3); //cambiar por mi cache de datos
                                                TicReloj();
                                            }
                                            else
                                            {
                                                c3 = true;
                                                TicReloj();
                                                inicioBloque = bloque * 4;    //inicio del bloque a copiar

                                                for (int i = 0; i < 4; ++i) //Copia los datos de memoria a Cache
                                                {
                                                    cacheDatos3[i, posicion] = memDatos[inicioBloque];
                                                    inicioBloque++;
                                                }
                                                cacheDatos3[4, posicion] = bloque;
                                                cacheDatos3[5, posicion] = 1;
                                                FallodeCache(28);
                                                Monitor.Exit(busD);
                                            }
                                        }
                                        else
                                        {
                                            conseguido = true;
                                        }
                                    }
                                    reg[rf2] = cacheDatos1[palabra, posicion];//Se le entrega el dato al registro
                                    Monitor.Exit(cacheDatos3); //soltar mi cache 
                                                               
                                    break;
                            }
                            break;

                        case 43: //SW

                            Console.WriteLine("Hace caso 43: SW");
                            int direccion = reg[rf1] + rd;
                            bloque = direccion / 16;
                            posicion = bloque % 4;
                            palabra = (direccion % 16) / 4;

                            switch (int.Parse(Thread.CurrentThread.Name))
                            {
                                case 1:
                                    //caculo de bloque y palabra
                                    bool conseguido = false;
                                    while (!conseguido)
                                    {
                                        while (!Monitor.TryEnter(cacheDatos1))
                                        {
                                            TicReloj();
                                        }
                                        TicReloj();

                                        if (!Monitor.TryEnter(busD))
                                        {
                                            Monitor.Exit(cacheDatos1); //cambiar por mi cache de datos
                                            TicReloj();

                                        }
                                        else
                                        {
                                            TicReloj();
                                            if (!Monitor.TryEnter(cacheDatos2))
                                            {
                                                Monitor.Exit(busD);
                                                Monitor.Exit(cacheDatos1);
                                                TicReloj();

                                            }
                                            else
                                            {
                                                TicReloj();
                                                if (bloque == cacheDatos2[4, posicion])
                                                {
                                                    cacheDatos2[5, posicion] = -1;
                                                }
                                                Monitor.Exit(cacheDatos2);

                                                if (!Monitor.TryEnter(cacheDatos3))
                                                {
                                                    Monitor.Exit(busD);
                                                    Monitor.Exit(cacheDatos1);
                                                    TicReloj();
                                                }
                                                else
                                                {
                                                    TicReloj();
                                                    if (bloque == cacheDatos3[4, posicion])
                                                    {
                                                        cacheDatos3[5, posicion] = -1;
                                                    }
                                                    Monitor.Exit(cacheDatos3);
                                                    conseguido = true;
                                                }
                                            }
                                        }
                                    }
                                    if ((bloque == cacheDatos1[4, posicion]) && (cacheDatos1[5, posicion] == 1))
                                    {
                                        cacheDatos1[palabra, posicion] = rf2;//registro donde viene

                                    }
                                    memDatos[palabra] = reg[rf2]; //registro donde viene
                                    FallodeCache(7);
                                    Monitor.Exit(busD);
                                    Monitor.Exit(cacheDatos1);
                                    break;

                                case 2:
                                    bool c4 = false;
                                    while (!c4)
                                    {
                                        while (!Monitor.TryEnter(cacheDatos2))
                                        {
                                            TicReloj();
                                        }
                                        TicReloj();

                                        if (!Monitor.TryEnter(busD))
                                        {
                                            Monitor.Exit(cacheDatos2);
                                            TicReloj();

                                        }
                                        else
                                        {
                                            TicReloj();
                                            if (!Monitor.TryEnter(cacheDatos1))
                                            {
                                                Monitor.Exit(busD);
                                                Monitor.Exit(cacheDatos2);
                                                TicReloj();

                                            }
                                            else
                                            {
                                                TicReloj();
                                                if (bloque == cacheDatos1[4, posicion])
                                                {
                                                    cacheDatos1[5, posicion] = -1;
                                                }
                                                Monitor.Exit(cacheDatos1);

                                                if (!Monitor.TryEnter(cacheDatos3))
                                                {
                                                    Monitor.Exit(busD);
                                                    Monitor.Exit(cacheDatos2);
                                                    TicReloj();
                                                }
                                                else
                                                {
                                                    TicReloj();
                                                    if (bloque == cacheDatos3[4, posicion])
                                                    {
                                                        cacheDatos3[5, posicion] = -1;
                                                    }
                                                    Monitor.Exit(cacheDatos3);
                                                    c4 = true;
                                                }
                                            }
                                        }
                                    }
                                    if ((bloque == cacheDatos2[4, posicion]) && (cacheDatos2[5, posicion] == 1))
                                    {
                                        cacheDatos2[palabra, posicion] = rf2;//registro donde viene

                                    }
                                    memDatos[palabra] = reg[rf2]; //registro donde viene
                                    FallodeCache(7);
                                    Monitor.Exit(busD);
                                    Monitor.Exit(cacheDatos2);  //sino es la 1
                                    break;

                                case 3:
                                    bool c5 = false;
                                    while (!c5)
                                    {
                                        while (!Monitor.TryEnter(cacheDatos3))
                                        {
                                            TicReloj();
                                        }
                                        TicReloj();

                                        if (!Monitor.TryEnter(busD))
                                        {
                                            Monitor.Exit(cacheDatos3);
                                            TicReloj();

                                        }
                                        else
                                        {
                                            TicReloj();
                                            if (!Monitor.TryEnter(cacheDatos1))
                                            {
                                                Monitor.Exit(busD);
                                                Monitor.Exit(cacheDatos3);
                                                TicReloj();

                                            }
                                            else
                                            {
                                                TicReloj();
                                                if (bloque == cacheDatos1[4, posicion])
                                                {
                                                    cacheDatos1[5, posicion] = -1;
                                                }
                                                Monitor.Exit(cacheDatos1);

                                                if (!Monitor.TryEnter(cacheDatos2))
                                                {
                                                    Monitor.Exit(busD);
                                                    Monitor.Exit(cacheDatos3);
                                                    TicReloj();
                                                }
                                                else
                                                {
                                                    TicReloj();
                                                    if (bloque == cacheDatos2[4, posicion])
                                                    {
                                                        cacheDatos2[5, posicion] = -1;
                                                    }
                                                    Monitor.Exit(cacheDatos2);
                                                    c5 = true;
                                                }
                                            }
                                        }
                                    }
                                    if ((bloque == cacheDatos3[4, posicion]) && (cacheDatos3[5, posicion] == 1))
                                    {
                                        cacheDatos3[palabra, posicion] = rf2;//registro donde viene

                                    }
                                    memDatos[palabra] = reg[rf2]; //registro donde viene
                                    FallodeCache(7);
                                    Monitor.Exit(busD);
                                    Monitor.Exit(cacheDatos3);  //sino es la 1
                                    break;
                            }
                            break;

                        case 63: //FIN
                            Console.WriteLine("Instruccion de FIN");
                            quantum = -1;  // Para tener el control de que la ultima instruccion fue FIN
                            break;
                    }

                    quantum--; //lo resto al finalizar una instruccion

                    if (quantum < 0)//ultima fue FIN
                    {

                        while (!Monitor.TryEnter(finalizados))
                        {
                            TicReloj();
                        }
                        TicReloj();

                        cpu += (reloj - inicioReloj);   //Ciclos de reloj que duro el hilillo en ejecucion
                        Console.WriteLine("Se agrego elemento a finalizados PC: ");
                        finalizados.GuardarFinalizados(PC, ref reg, cpu, reloj);
                        Monitor.Exit(finalizados);
                    }
                    else
                    {
                        if (quantum == 0)//Se termino el quantum
                        {
                            while (!Monitor.TryEnter(cola))
                            {
                                TicReloj();
                            }
                            TicReloj();
                            cpu += (reloj - inicioReloj);
                            cola.Guardar(PC, ref reg, cpu);
                            Console.WriteLine("Se guardo contexto \n");
                            Monitor.Exit(cola);
                        }
                    }
                    TicReloj();

                }//FIN del quantum
            }//FIN del while(true)
        } //FIN de Nucleos 
    }//FIN de la clase Nucleos



    public class Contextos
    {
        private static Queue queue;
        private int contador;
        private struct Contexto // C# mantiene los struct
        {
            public int pc;
            public int[] regist;
            public int relojCPU;
            public int relojTotal;

            public Contexto(int p, ref int[] reg, int cpu)  //Contextos con registros que no han finalizado
            {
                pc = p;
                regist = new int[32];
                relojCPU = cpu;
                relojTotal = 0;

                for (int i = 1; i < 32; ++i)
                {
                    regist[i] = reg[i];
                }
            }

            public Contexto(int p)      //Contextos que solo tiene el PC
            {
                pc = p;
                relojCPU = 0;
                relojTotal = 0;           
                regist = new int[32];
                for (int i = 1; i < 32; ++i)
                {
                    regist[i] = 0;
                }
            }

            public Contexto(int p, ref int[] reg, int cpu, int total)   //Contextos finalizados
            {
                pc = p;
                regist = new int[32];
                relojCPU = cpu;
                relojTotal = total;

                for (int i = 1; i < 32; ++i)
                {
                    regist[i] = reg[i];
                }
            }
        }//FIN del struct

        public Contextos()
        {
            queue = new Queue();
        }

        ~Contextos() //Destructor de la clase
        {
            //cola.Finalize();
        }


        //reg se debe recibir por referencia 
        public void Guardar(int p, ref int[] reg, int cpu)//Guarda el contexto         
        {
            Contexto nueva = new Contexto(p, ref reg, cpu);
            queue.Enqueue(nueva);
            contador++;

        }//FIN de Guardar

        public void Sacar(out int p, ref int[] reg, ref int relojActual)//Retorna el contexto
        {
            Contexto aux = (Contexto)queue.Dequeue();
            for (int i = 1; i < 32; ++i)
            {
                reg[i] = aux.regist[i];
            }
            relojActual += aux.relojCPU;
            p = aux.pc;
            contador--;
        }//FIN de Sacar

        public void Encolar(int p)
        {
            Contexto nueva = new Contexto(p);
            queue.Enqueue(nueva);
            contador++;
        }//FIN de Encolar

        public int Cantidad()
        {
            return contador;

        }//FIN de cantidad

        public void GuardarFinalizados(int p, ref int[] reg, int cpu, int total)
        {
            Contexto nueva = new Contexto(p, ref reg, cpu, total);
            queue.Enqueue(nueva);
            contador++;
        }//FIN de GuardarFinalizados

        public void Imprimir()
        {
            while(0 < contador)
            {
                Contexto aux = (Contexto)queue.Dequeue();
                contador--;
                Console.WriteLine("PC: \t" + aux.pc + "\nReloj CPU: \t" + aux.relojCPU + "\nReloj Total: \t" + aux.relojTotal + "\n");
                for (int i = 0; i < 32; ++i)
                {
                    Console.WriteLine("reg[" + i + "]= \t" + aux.regist[i]);
                }
                string t = Console.ReadLine();

            }            
        }

    }//FIN de la clase Contextos
}//FIN del namespace