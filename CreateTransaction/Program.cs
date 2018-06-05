using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Neo;
using Neo.Core;
using Neo.Implementations.Blockchains.LevelDB;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using VMArray = Neo.VM.Types.Array;

namespace ConsoleApp2
{
    class Program
    {
        static void Main(string[] args)
        {
            //Need libleveldb.dll, and requires a platform(x86 or x64) that is consistent with the program.
            //Path of blockchain folder
            Blockchain.RegisterBlockchain(new LevelDBBlockchain("C:\\Users\\chenz\\Documents\\1Code\\chenzhitong\\neo-cli\\neo-cli\\bin\\Debug\\netcoreapp2.0\\Chain_00746E41"));

            var tx = CreateNep5Transfer();
            if (tx == null)
            {
                Console.WriteLine("Failed");
                Console.ReadLine();
                return;
            }
            Console.WriteLine(tx.ToJson().ToString());

            var wallet = new Neo.Implementations.Wallets.NEP6.NEP6Wallet("wallet.json");
            try
            {
                wallet.Unlock("password");
            }
            catch (Exception)
            {
                Console.WriteLine("password error");
            }

            //Signature
            var result = wallet.Sign(new ContractParametersContext(tx));
            if (result)
            {
                Console.WriteLine("Signature successful");
            }
            else
            {
                Console.WriteLine("Signature failed");
            }
            Console.ReadLine();
        }

        //Transfer Global Asset
        public static Transaction CreateGlobalTransfer()
        {
            var outputs = new List<TransactionOutput>{ new TransactionOutput()
            {
                AssetId = Blockchain.GoverningToken.Hash, //Asset Id, this is NEO
                ScriptHash = Wallet.ToScriptHash("AS8UDW7aLhrywLVHFL3ny5tSBaVhWTeZjT"), //Receiver
                Value = new Fixed8(100000000) //Value (satoshi unit)
            }}.ToArray();
            var inputs = new List<CoinReference> {
                //coin reference A
                new CoinReference(){
                    PrevHash = new UInt256("3631f66024ca6f5b033d7e0809eb993443374830025af904fb51b0334f127cda".HexToBytes().Reverse().ToArray()),
                    PrevIndex = 0
                },
                //coin reference B
                new CoinReference(){
                    PrevHash = new UInt256("e356e704b4321654523816c5059709d461e566c8c834af7435b9a47cd4ad0377".HexToBytes().Reverse().ToArray()),
                    PrevIndex = 0
                }
            }.ToArray();

            return new ContractTransaction()
            {
                Outputs = outputs,
                Inputs = inputs,
                Attributes = new TransactionAttribute[0],
                Scripts = new Witness[0]
            };
        }

        //Transfer NEP-5 Asset
        public static Transaction CreateNep5Transfer()
        {
            var from = Wallet.ToScriptHash("AS8UDW7aLhrywLVHFL3ny5tSBaVhWTeZjT");
            var assetId = new UInt160("ceab719b8baa2310f232ee0d277c061704541cfb".HexToBytes().Reverse().ToArray());
            var to = Wallet.ToScriptHash("AS8UDW7aLhrywLVHFL3ny5tSBaVhWTeZjT");
            var value = 100;

            //Query Balances
            using (ScriptBuilder sb2 = new ScriptBuilder())
            {
                byte[] script;
                sb2.EmitAppCall(assetId, "balanceOf", from);
                sb2.Emit(OpCode.DEPTH, OpCode.PACK);
                script = sb2.ToArray();
                ApplicationEngine engine = ApplicationEngine.Run(script);
                if (engine.State.HasFlag(VMState.FAULT)) return null;
                var balances = ((VMArray)engine.EvaluationStack.Pop())[0];
                BigInteger sum = balances.GetBigInteger();
                if (sum < value)
                {
                    Console.WriteLine("Insufficient balance");
                    return null;
                }
            }

            //Transfer
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitAppCall(assetId, "transfer", from, to, value);
                sb.Emit(OpCode.THROWIFNOT);

                byte[] nonce = new byte[8];
                Random rand = new Random();
                rand.NextBytes(nonce);
                sb.Emit(OpCode.RET, nonce);
                return new InvocationTransaction
                {
                    Version = 1,
                    Script = sb.ToArray(),
                    Outputs = new TransactionOutput[0],
                    Inputs = new CoinReference[0],
                    Attributes = new TransactionAttribute[0],
                    Scripts = new Witness[0]
                };
            }
        }
    }
}
