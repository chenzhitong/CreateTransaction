using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo;
using Neo.Core;
using Neo.Implementations.Blockchains.LevelDB;
using Neo.Wallets;

namespace ConsoleApp2
{
    class Program
    {
        static void Main(string[] args)
        {
            var tx = CreateGlobalTransfer();
            //Need libleveldb.dll, and requires a platform(x86 or x64) that is consistent with the program.
            //Path of blockchain folder
            Blockchain.RegisterBlockchain(new LevelDBBlockchain("C:\\Users\\chenz\\Documents\\1Code\\chenzhitong\\neo-cli\\neo-cli\\bin\\Debug\\netcoreapp2.0\\Chain_00746E41"));
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
            var result = wallet.Sign(new Neo.SmartContract.ContractParametersContext(tx)); 
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

        public static Transaction CreateGlobalTransfer()
        {
            var outputs = new List<TransactionOutput>{ new TransactionOutput()
            {
                AssetId = Blockchain.GoverningToken.Hash, //Asset Id, this is NEO
                ScriptHash = Wallet.ToScriptHash("AS8UDW7aLhrywLVHFL3ny5tSBaVhWTeZjT"), //Receiver
                Value = new Fixed8(100000000) //Value (satoshi unit)
            }}.ToArray();
            var inputs = new List<CoinReference> {new CoinReference(){
                PrevHash = new UInt256("3631f66024ca6f5b033d7e0809eb993443374830025af904fb51b0334f127cda".HexToBytes().Reverse().ToArray()),
                PrevIndex = 0
            }}.ToArray();
            
            return new ContractTransaction()
            {
                Outputs = outputs,
                Inputs = inputs,
                Attributes = new TransactionAttribute[0],
                Scripts = new Witness[0]
            };
        }

        public static Transaction CreateNep5Transfer()
        {
            var wallet = new Neo.Implementations.Wallets.NEP6.NEP6Wallet("wallet.json");
            try
            {
                wallet.Unlock("password");
            }
            catch (Exception)
            {
                Console.WriteLine("password error");
            }
            var assetId = new UInt160("ceab719b8baa2310f232ee0d277c061704541cfb".HexToBytes().Reverse().ToArray());
            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            if (!BigDecimal.TryParse("100", descriptor.Decimals, out BigDecimal amount))
            {
                Console.WriteLine("Incorrect Amount Format");
                return null;
            }
            return wallet.MakeTransaction(null, new[]
                {
                    new TransferOutput
                    {
                        AssetId = assetId,
                        Value = amount,
                        ScriptHash = Wallet.ToScriptHash("AS8UDW7aLhrywLVHFL3ny5tSBaVhWTeZjT")
                    }
                }, fee: Fixed8.Zero);
        }
    }
}
