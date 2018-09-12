﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Neo;
using Neo.Core;
using Neo.Implementations.Blockchains.LevelDB;
using Neo.IO;
using Neo.Network;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using VMArray = Neo.VM.Types.Array;

namespace Demo
{
    public delegate Transaction SignDelegate(Transaction tx);

    class Program
    {
        static void Main(string[] args)
        {
            //Need libleveldb.dll, and requires a platform(x86 or x64) that is consistent with the program.
            //Path of blockchain folder
            Blockchain.RegisterBlockchain(new LevelDBBlockchain("D:\\PrivateNet2\\node1\\Chain_0001E240"));

            SignDelegate sign = new SignDelegate(SignWithWallet);

            var tx = CreateGlobalTransfer(sign);
            
            //Console.WriteLine(tx.ToArray().ToHexString());
            Console.WriteLine("交易验证：" + tx.Verify(new List<Transaction> { tx }));
            Console.WriteLine(tx.ToArray().ToHexString());
            //然后调用 neo-cli 的 API：sendrawtransaction 

            Console.ReadLine();
        }

        private static Transaction SignWithWallet(Transaction tx)
        {
            if (tx == null)
            {
                throw new ArgumentNullException("tx");
            }
            tx.ToJson();


            var wallet = new Neo.Implementations.Wallets.NEP6.NEP6Wallet(new WalletIndexer("D:\\PrivateNet2\\node1\\Index_0001E240"), "1.json");
            try
            {
                wallet.Unlock("11111111");
            }
            catch (Exception)
            {
                Console.WriteLine("password error");
            }

            //Signature
            var context = new ContractParametersContext(tx);
            wallet.Sign(context);
            if (context.Completed)
            {
                Console.WriteLine("签名成功");
                tx.Scripts = context.GetScripts();
            }
            else
            {
                Console.WriteLine("签名失败");
            }
            //Console.WriteLine(tx.ToArray().ToHexString());
            Console.WriteLine("交易验证：" + tx.Verify(new List<Transaction> { tx }));
            Console.WriteLine(tx.ToArray().ToHexString());
            return tx;
        }

        //Transfer Global Asset
        public static Transaction CreateGlobalTransfer(SignDelegate sign)
        {
            //交易输入是 1 GAS
            var inputs = new List<CoinReference> {
                //coin reference A
                new CoinReference(){
                    PrevHash = new UInt256("0x323610d6e89aae0f7b75774fcbabf9e6a98e9d61340c55e35e702159b0115ba9".Remove(0, 2).HexToBytes().Reverse().ToArray()),
                    PrevIndex = 1
                }
            }.ToArray();
            //交易输出是 0.999 GAS，找回到原地址
            var outputs = new List<TransactionOutput>{ new TransactionOutput()
            {
                AssetId = Blockchain.UtilityToken.Hash, //Asset Id, this is GAS
                ScriptHash = Wallet.ToScriptHash("Ad1HKAATNmFT5buNgSxspbW68f4XVSssSw"), //Receiver
                Value = new Fixed8((long)(203 * (long)Math.Pow(10, 8))) //Value (satoshi unit)
            }}.ToArray();
            //则手续费是 0.001 GAS

            var tx = new ContractTransaction()
            {
                Outputs = outputs,
                Inputs = inputs,
                Attributes = new TransactionAttribute[0],
                Scripts = new Witness[0],
            };
            return sign.Invoke(tx);
        }

        public static Transaction Claim(Wallet wallet, SignDelegate sign)
        {
            CoinReference[] claims = wallet.GetUnclaimedCoins().Select(p => p.Reference).ToArray();
            if (claims.Length == 0) return null;

            ClaimTransaction tx = new ClaimTransaction
            {
                Claims = claims,
                Attributes = new TransactionAttribute[0],
                Inputs = new CoinReference[0],
                Outputs = new[]
                {
                    new TransactionOutput
                    {
                        AssetId = Blockchain.UtilityToken.Hash,
                        Value = Blockchain.CalculateBonus(claims),
                        ScriptHash = wallet.GetChangeAddress()
                    }
                }

            };

            //交易输入是 1 GAS
            var input = new CoinReference()
            {
                PrevHash = new UInt256("0x51ac4f7f1662d8c9379ccce3fa7cd2085b9a865edfa53ad892352a41768dd1de".Remove(0, 2).HexToBytes().Reverse().ToArray()),
                PrevIndex = 0
            };
            //交易输出是 0.999 GAS，找回到原地址
            var output = new TransactionOutput()
            {
                AssetId = Blockchain.UtilityToken.Hash, //Asset Id, this is NEO
                ScriptHash = Wallet.ToScriptHash("AJd31a8rYPEBkY1QSxpsGy8mdU4vTYTD4U"), //Receiver
                Value = new Fixed8((long)(0.999 * (long)Math.Pow(10, 8))) //Value (satoshi unit)
            };
            //则手续费是 0.001 GAS
            tx.Inputs.ToList().Add(input);
            tx.Outputs.ToList().Add(output);

            return sign.Invoke(tx);
        }

        //Transfer NEP-5 Asset
        public static Transaction CreateNep5Transfer(SignDelegate sign)
        {
            var from = Wallet.ToScriptHash("AS8UDW7aLhrywLVHFL3ny5tSBaVhWTeZjT");
            var assetId = new UInt160("ceab719b8baa2310f232ee0d277c061704541cfb".HexToBytes().Reverse().ToArray());
            var to = Wallet.ToScriptHash("AS8UDW7aLhrywLVHFL3ny5tSBaVhWTeZjT");
            var value = 100;

            //交易输入是 1 GAS
            var inputs = new List<CoinReference> {
                //coin reference A
                new CoinReference(){
                    PrevHash = new UInt256("0x51ac4f7f1662d8c9379ccce3fa7cd2085b9a865edfa53ad892352a41768dd1de".Remove(0, 2).HexToBytes().Reverse().ToArray()),
                    PrevIndex = 0
                }
            }.ToArray();
            //交易输出是 0.999 GAS，找回到原地址
            var outputs = new List<TransactionOutput>{ new TransactionOutput()
            {
                AssetId = Blockchain.UtilityToken.Hash, //Asset Id, this is NEO
                ScriptHash = Wallet.ToScriptHash("AJd31a8rYPEBkY1QSxpsGy8mdU4vTYTD4U"), //Receiver
                Value = new Fixed8((long)(0.999 * (long)Math.Pow(10, 8))) //Value (satoshi unit)
            }}.ToArray();
            //则手续费是 0.001 GAS

            //Query Balances
            using (ScriptBuilder sb2 = new ScriptBuilder())
            {
                byte[] script;
                sb2.EmitAppCall(assetId, "balanceOf", from);
                sb2.Emit(OpCode.DEPTH, OpCode.PACK);
                script = sb2.ToArray();
                ApplicationEngine engine = ApplicationEngine.Run(script);
                if (engine.State.HasFlag(VMState.FAULT)) return null;
                var balances = ((VMArray)engine.ResultStack.Pop())[0];
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
                var tx = new InvocationTransaction
                {
                    Version = 1,
                    Script = sb.ToArray(),
                    Outputs = outputs,
                    Inputs = inputs,
                    Attributes = new TransactionAttribute[0],
                    Scripts = new Witness[0]
                };
                return sign.Invoke(tx);
            }
        }
    }
}
