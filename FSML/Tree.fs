module Tree

    open DataTypes
    open Utilities
    open MathNet.Numerics
    open MathNet.Numerics.LinearAlgebra
    open MathNet.Numerics.Random

    type node = {mutable nodeId:int; mutable featureId:int; mutable splitValue:double; mutable leafValue:double}

    type tree<'node>  =
        | Empty
        | TreeNode of 'node * 'node tree* 'node tree

    let rec getHeight (tree: tree<'a>) =
        match tree with
        | Empty -> 0
        | TreeNode(head,left,right) -> 1 + max (getHeight left) (getHeight right)
    
    let getDepth (tree: tree<'a>) = -1 + getHeight tree

    let rec predictTree tree (x:Vector<double>)= 
        match tree, x with
        | Empty,_ -> 0.0
        | TreeNode(head,left,right),_ ->
            match left with
            | Empty -> head.leafValue
            | _ -> if x.[head.featureId]< head.splitValue then predictTree left x else predictTree right x
    
    let predictForestforVector forest (x:Vector<double>)  = forest |> Array.map (fun tree -> predictTree tree x ) |> Array.sum
    let predictForestforMatrix forest (x:Matrix<double>)  = 
        x.EnumerateRows() |> Seq.map (fun row -> predictForestforVector forest row) |> Array.ofSeq

    let gh_lm (y:double) (pred:double) = 
        pred - y, 1.0
    
    let gh_lr (y:double) (pred:double)=
        pred - y, max (pred * (1.0 - pred)) 1e-16


    let splitNode (fInTree: bool[], xInNode: bool [], xValueSorted: double [][], xIndexSorted: int [][],gTilde: double [],hTilde: double [],lambda:double,gamma:double) = 
        let mutable score= 0.0
        let mutable bestFeature,bestBreak,bestIndex = 0,0.0,0
        let mutable wLeft, wRight = 0.0,0.0
        let mutable doSplit = false
        let ncol =fInTree.Length

        let g = xInNode |> Array.mapi (fun i e -> if e then gTilde.[i] else 0.0) |> Array.sum
        let h = xInNode |> Array.mapi (fun i e -> if e then hTilde.[i] else 0.0) |> Array.sum

        for k in [0..ncol-1] do
            if fInTree.[k] then
                let mutable gLeft,gRight,hLeft,hRight = 0.0,0.0,0.0,0.0
                for j in xIndexSorted.[k] do
                    let index = xIndexSorted.[k].[j]
                    if xInNode.[index] then
                        gLeft <- gLeft + gTilde.[index]
                        gRight <- g - gLeft
                        hLeft <- hLeft + hTilde.[index]
                        hRight <- h - hLeft

                        let scoreNew = (gLeft * gLeft)/(hLeft+lambda) + (gRight*gRight)/(hRight+lambda) - (g*g)/(h+lambda)
                        if scoreNew > score then
                            doSplit <- true
                            score <- scoreNew
                            bestFeature <- k
                            bestBreak <- xValueSorted.[k].[j]
                            bestIndex <- j
                            wLeft <- - gLeft/(hLeft + lambda)
                            wRight <- - gRight/(hRight + lambda)

        doSplit && (0.5*score > gamma),bestFeature,bestBreak,bestIndex,wLeft,wRight,score

    let buildTree (featureIndex: int [],xValueSorted: double [][], xIndexSorted: int [][], y: double [],yTilde: double [],gTilde: double [] ref,hTilde: double [] ref, depth:int,eta:double,lambda:double,gamma:double,sub_sample:double,sub_feature:double)=
        let ncol = featureIndex.Length
        let nrow = y.Length
        let rowSelected = Random.doubles nrow |> Array.map( fun e -> e <= sub_sample)
        let colSelected = Random.doubles ncol |> Array.map( fun e -> e <= sub_feature)
        let mutable score = 0.0

        0

    let rec growTree (currentTree: tree<node>) (fInTree: bool []) (xInNode: bool []) (maxDepth:int) (xValueSorted: double [][]) (xIndexSorted: int [][]) (y: double []) (yTilde: double []) (gTilde: double []) (hTilde: double []) (eta:double) (lambda:double) (gamma:double)=
        let ncol = fInTree.Length
        let nrow = y.Length
        let mutable currentNodeId = 0
        if maxDepth = 0 then currentTree
        else 
            match currentTree with
            | Empty -> 
                let doSplit,bestFeature,bestBreak,bestIndex,wLeft,wRight,score = splitNode (fInTree,xInNode,xValueSorted,xIndexSorted,gTilde,hTilde,lambda,gamma)
                if doSplit then
                    let xInLeftNode = xIndexSorted.[bestFeature] |> Array.mapi (fun i e -> if (xInNode.[i] && i <= bestIndex) then true else false )
                    let xInRightNode = xIndexSorted.[bestFeature] |> Array.mapi (fun i e -> if (xInNode.[i] && i > bestIndex) then true else false )
                    for i in [0..nrow-1] do
                        if xInLeftNode.[i] then
                            yTilde.[i] <- wLeft
                            let gt,ht= gh_lm y.[i] wLeft
                            gTilde.[i] <- gt 
                            hTilde.[i] <- ht
                        if xInRightNode.[i] then
                            yTilde.[i] <- wRight
                            let gt,ht= gh_lm y.[i] wRight
                            gTilde.[i] <- gt 
                            hTilde.[i] <- ht                  
                    let currentNode = {nodeId=currentNodeId; featureId=bestFeature;splitValue=bestBreak;leafValue=0.0}
                    do currentNodeId <- currentNodeId + 1
                    let mutable leftNode = TreeNode({nodeId=currentNodeId; featureId= -1;splitValue=0.0;leafValue=wLeft},Empty,Empty)
                    leftNode <- growTree leftNode fInTree xInLeftNode (maxDepth-1) xValueSorted xIndexSorted y yTilde gTilde hTilde eta lambda gamma
                    do currentNodeId <- currentNodeId + 1
                    let mutable rightNode = TreeNode({nodeId=currentNodeId; featureId= -1;splitValue=0.0;leafValue=wRight},Empty,Empty)
                    rightNode <- growTree rightNode fInTree xInRightNode (maxDepth-1) xValueSorted xIndexSorted y yTilde gTilde hTilde eta lambda gamma
                    TreeNode(currentNode,leftNode,rightNode)
                else Empty
            | TreeNode(head,left,right) ->
                match left with
                | Empty -> 
                    let doSplit,bestFeature,bestBreak,bestIndex,wLeft,wRight,score = splitNode (fInTree,xInNode,xValueSorted,xIndexSorted,gTilde,hTilde,lambda,gamma)
                    if doSplit then
                        let xInLeftNode = xIndexSorted.[bestFeature] |> Array.mapi (fun i e -> if (xInNode.[i] && i <= bestIndex) then true else false )
                        let xInRightNode = xIndexSorted.[bestFeature] |> Array.mapi (fun i e -> if (xInNode.[i] && i > bestIndex) then true else false )
                        for i in [0..nrow-1] do
                            if xInLeftNode.[i] then
                                yTilde.[i] <- wLeft
                                let gt,ht= gh_lm y.[i] wLeft
                                gTilde.[i] <- gt 
                                hTilde.[i] <- ht
                            if xInRightNode.[i] then
                                yTilde.[i] <- wRight
                                let gt,ht= gh_lm y.[i] wRight
                                gTilde.[i] <- gt 
                                hTilde.[i] <- ht 
                        do currentNodeId <- currentNodeId + 1
                        let currentNode = {nodeId=currentNodeId; featureId=bestFeature;splitValue=bestBreak;leafValue=0.0}
                        do currentNodeId <- currentNodeId + 1
                        let mutable leftNode = TreeNode({nodeId=currentNodeId; featureId= -1;splitValue=0.0;leafValue=wLeft},Empty,Empty)
                        leftNode <- growTree leftNode fInTree xInLeftNode (maxDepth-1) xValueSorted xIndexSorted y yTilde gTilde hTilde eta lambda gamma
                        do currentNodeId <- currentNodeId + 1
                        let mutable rightNode = TreeNode({nodeId=currentNodeId; featureId= -1;splitValue=0.0;leafValue=wRight},Empty,Empty)
                        rightNode <- growTree rightNode fInTree xInRightNode (maxDepth-1) xValueSorted xIndexSorted y yTilde gTilde hTilde eta lambda gamma
                        TreeNode(currentNode,leftNode,rightNode)
                    else Empty
                | _ -> Empty
        //let rowSelected = Random.doubles nrow |> Array.map( fun e -> e <= sub_sample)
        //let colSelected = Random.doubles ncol |> Array.map( fun e -> e <= sub_feature)




        
