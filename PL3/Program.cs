using System.Threading.Channels;

namespace Lab3
{
    class TokenRingUnit
    {
        Channel<Token>? IncomeCh;
        Channel<Token>? OutcomeCh;
        int UnitId;

        public TokenRingUnit(int id)
        {
            UnitId = id;
        }

        public void setWriter(Channel<Token> writer)
        {
            OutcomeCh = writer;
        }

        public void setReader(Channel<Token> reader)
        {
            IncomeCh = reader;
        }

        //узлы, кроме 0-го, переходят в режим ожидания
        public async void initWaiting()
        {
            await waitToken();
        }

        //начать пересылку токена с 0-го узла
        public async Task initTokenSending(Token token)
        {
            await sendToken(token);
            await waitToken();
        }

        //отправить токен
        public async Task sendToken(Token token)
        {
            if (OutcomeCh != null)
            {
                await OutcomeCh.Writer.WriteAsync(token);
                Console.WriteLine("Token was sent from unit " + UnitId);
            }
            else throw new Exception($"Caught a sending error on unit {UnitId}") ;
        }

        //ожидание сообщения от предыдущего узла в кольце + отправка на обработку
        public async Task waitToken()
        {
            for (; ; )
            {
                if (IncomeCh != null)
                {                 
                    await IncomeCh.Reader.WaitToReadAsync();
                    Token token = await IncomeCh.Reader.ReadAsync();
                    await processToken(token);
                }

                else throw new Exception($"Caught a receiving error on unit {UnitId}");
            }
        }
      
        //обработать токен
        public async Task processToken(Token token)
        {
            Console.WriteLine("Token is in processing at unit " + UnitId);
            token.ttl--;

            if (token.recipient == UnitId)
                Console.WriteLine("That's what I was waiting for! Let's look: " + token.data);
           
            else if (token.ttl>0)
            {               
                await sendToken(token);
            }

            else Console.WriteLine("Token time of life is over!");
        }
    }

    struct Token
    {
        public string data;
        public int recipient;
        public int ttl;
    }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            string message = "";
            int targetUnit=0, mesLifeTime=0, amountOfTokenUnits=0;
            bool isCorrect = false;

            //запрашиваем у пользователя входные данные
            while (!isCorrect)
            {
                Console.WriteLine("Please, type needful amount of TokenRing units (int > 1)");
                isCorrect = int.TryParse(Console.ReadLine(), out amountOfTokenUnits) && amountOfTokenUnits > 1;
            }

            isCorrect = false;

            while (!isCorrect)
            {
                Console.WriteLine("Please, type token time of life (int > 0)");
                isCorrect = int.TryParse(Console.ReadLine(), out mesLifeTime) && mesLifeTime > 0;
            }

            isCorrect = false;

            while (!isCorrect)
            {
                Console.WriteLine("Please, type id of unit-recipient (0<=int<amount of TokenRing units)");
                isCorrect = int.TryParse(Console.ReadLine(), out targetUnit) && targetUnit >= 0 && targetUnit < amountOfTokenUnits;
            }

            isCorrect = false;

            while (!isCorrect)
            {
                Console.WriteLine("Please, type your message (not empty)");
                message = Console.ReadLine();
                isCorrect = !(string.IsNullOrEmpty(message));              
            }

            //формируем токен, базируясь на данных пользователя
            Token token = new Token
            {
                data = message,
                recipient = targetUnit,
                ttl = mesLifeTime,
            };

            //создаём необходимые узлы
            TokenRingUnit[] units = new TokenRingUnit[amountOfTokenUnits];
            units[0] = new TokenRingUnit(0);

            for (int i = 0; i < amountOfTokenUnits - 1; i++)
            {
                Channel<Token> newChannel = Channel.CreateBounded<Token>(1);              
                Channel<Token> channel = newChannel;
             
                units[i + 1] = new TokenRingUnit(i+1);
                units[i].setWriter(channel);
                units[i + 1].setReader(channel);
            }

            //замыкаем кольцо
            Channel<Token> lastChannel = Channel.CreateBounded<Token>(1);
            Channel<Token> ch = lastChannel;
            units[0].setReader(ch);
            units[amountOfTokenUnits - 1].setWriter(ch);

            //ставим все узлы, кроме 0-го, в режим ожидания
            for (int i = 1; i < amountOfTokenUnits; i++)
            {
                Thread thr = new Thread(units[i].initWaiting); 
                thr.Start();
            }

            //отправляем токен с 0-го узла
            await units[0].initTokenSending(token);
        }
    }
}