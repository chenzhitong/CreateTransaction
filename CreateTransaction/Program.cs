using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Persistence.LevelDB;
using Neo.IO;
using Neo.Network;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using VMArray = Neo.VM.Types.Array;
using Neo.Ledger;
using Neo.Wallets.NEP6;
using Neo.Persistence;

namespace Demo
{
    public delegate Transaction SignDelegate(Transaction tx);

    class Program
    {
        static void Main(string[] args)
        {
            var system = new NeoSystem(new LevelDBStore("D:\\PrivateNet2\\NEO-GUI 2.9 release\\Chain_0001E240"));

            SignDelegate sign = new SignDelegate(SignWithWallet);

            var tx = CreateGlobalTransfer(sign);
            Console.ReadLine();
        }

        private static Transaction SignWithWallet(Transaction tx)
        {
            if (tx == null)
            {
                throw new ArgumentNullException("tx");
            }
    try
    {
        tx.ToJson();
    }
    catch (Exception)
    {
        throw new FormatException("交易格式错误");
    }


            var wallet = new NEP6Wallet(new WalletIndexer("D:\\PrivateNet2\\node1\\Index_0001E240"), "1.json");
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
                tx.Witnesses = context.GetWitnesses();
            }
            else
            {
                Console.WriteLine("签名失败");
            }
            //Console.WriteLine(tx.ToArray().ToHexString());
            Console.WriteLine("交易验证：" + tx.Verify(Blockchain.Singleton.GetSnapshot(), new List<Transaction> { tx }));
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
                    PrevHash = new UInt256("0x21b64eb35881e7261c72c70f38bd6d5eb6aa18f232e08ba3022220b46c13d9a2".Remove(0, 2).HexToBytes().Reverse().ToArray()),
                    PrevIndex = 0
                }
            }.ToArray();
            //交易输出是 0.999 GAS，找回到原地址
            var outputs = new List<TransactionOutput>{ new TransactionOutput()
            {
                AssetId = Blockchain.UtilityToken.Hash, //Asset Id, this is GAS
                ScriptHash = "Ad1HKAATNmFT5buNgSxspbW68f4XVSssSw".ToScriptHash(), //Receiver
                Value = new Fixed8((long)(0.999 * (long)Math.Pow(10, 8))) //Value (satoshi unit)
            }}.ToArray();
            //则手续费是 0.001 GAS

            var tx = new ContractTransaction()
            {
                Outputs = outputs,
                Inputs = inputs,
                Attributes = new TransactionAttribute[0],
                Witnesses = new Witness[0],
            };
            return sign.Invoke(tx);
        }

        public static Transaction Claim(Wallet wallet, SignDelegate sign)
        {
            CoinReference[] claims = wallet.GetUnclaimedCoins().Select(p => p.Reference).ToArray();
            if (claims.Length == 0) return null;

            using (Snapshot snapshot = Blockchain.Singleton.GetSnapshot())
            {
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
                            Value = snapshot.CalculateBonus(claims),
                            ScriptHash = wallet.GetChangeAddress()
                        }
                    }

                };

                return sign.Invoke(tx);
            }
        }

        //Transfer NEP-5 Asset
        public static Transaction CreateNep5Transfer(SignDelegate sign)
        {
            var from = "AS8UDW7aLhrywLVHFL3ny5tSBaVhWTeZjT".ToScriptHash();
            var assetId = new UInt160("ceab719b8baa2310f232ee0d277c061704541cfb".HexToBytes().Reverse().ToArray());
            var to = "AS8UDW7aLhrywLVHFL3ny5tSBaVhWTeZjT".ToScriptHash();
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
                ScriptHash = "AJd31a8rYPEBkY1QSxpsGy8mdU4vTYTD4U".ToScriptHash(), //Receiver
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
                    Witnesses = new Witness[0]
                };
                return sign.Invoke(tx);
            }
        }
    }
}
