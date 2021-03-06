﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Boogie;

public class Diagnoser : StandardVisitor
{
    Program prog;
    Predicate<string> isBvOp;
    Predicate<string> isLoadStore;
    Dictionary<string, int> mapSizes;
    bool maybeImprecise;

	public Diagnoser(Program prog)
	{
        this.prog = prog;
        var bvOps = new HashSet<string>() { "$and", "$or", "$lshr", "$ashr", "$shl", "$xor", "$nand", "$not"};
        var loadStores = new HashSet<string>() { "$load", "$store" };
        isBvOp = (string func) => { return bvOps.Contains(func.Split('.')[0]); };
        isLoadStore = (string func) => { return loadStores.Contains(func.Split('.')[0]); };
        mapSizes = new Dictionary<string, int>();
        foreach (var map in prog.GlobalVariables.Where(g => g.TypedIdent.Type.IsMap))
        {
            mapSizes[map.Name] = 0;
        }
        maybeImprecise = false;
    }

    public bool getPrecision()
    {
        return maybeImprecise;
    }

    public Dictionary<string, int> getMapSizes()
    {
        return mapSizes;
    }

    public void Diagnose()
    {
        prog.Implementations.Iter(impl => VisitImplementation(impl));
        foreach (var item in mapSizes)
        {
            var map = item.Key;
            var size = item.Value;
            if (size == -1)
            {
                Console.WriteLine("Need byte precise: " + map);
            }
            else if (size == -2)
            {
                Console.WriteLine("Only pointers are stored in map: " + map);
            }
        }
    }

    public override Expr VisitNAryExpr(NAryExpr node)
    {
        if (isBvOp(node.Fun.FunctionName))
        {
            if (!node.Fun.FunctionName.Contains("bv"))
            {
                int length;
                if (int.TryParse(node.Fun.FunctionName.Split('.')[1].Split('i')[1], out length))
                {
                    if (length > 1)
                    {
                        Console.WriteLine("Catch a bv op at " + node.ToString());
                        maybeImprecise = true;
                    }
                }
                else
                {
                    Console.WriteLine("Cannot parse int: " + node.Fun.FunctionName.Split('.')[1].Split('i')[1]);
                }
            }
        }
        if (isLoadStore(node.Fun.FunctionName))
        {
            int width;
            if (node.Fun.FunctionName.Contains("ref"))
                width = -2;
            else if (node.Fun.FunctionName.Contains("float"))
                width = 32;
            else
            {
                var type = node.Fun.FunctionName.Contains("bytes")? node.Fun.FunctionName.Split('.')[2] : node.Fun.FunctionName.Split('.')[1];
                var prefix = type.StartsWith("i") ? "i" : "bv";
                if (int.TryParse(type.Substring(prefix.Length), out width))
                {
                    // pass
                }
                else
                {
                    Console.WriteLine("Cannot parse int: " + node.Fun.FunctionName.Split('.')[1].Split('i')[1]);
                    return base.VisitNAryExpr(node);
                }
            }
            var map = node.Args.Where(arg => mapSizes.Keys.Contains(arg.ToString())).First().ToString();
            var size = mapSizes[map];
            if (size == 0)
            {
                mapSizes[map] = width;
            }
            else
            {
                if (size != width)
                {
                    mapSizes[map] = -1;
                }
            }
        }
        return base.VisitNAryExpr(node);
    }
}
