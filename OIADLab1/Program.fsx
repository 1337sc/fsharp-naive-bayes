open System
open System.Collections.Generic
open System.Globalization
open CsvHelper
open CsvHelper.Configuration
open System.IO
open System.Linq
open XPlot.GoogleCharts

let getData maxRowsCount =
    let dataPath = "../../../data/data.csv"
    use dataFile = new StreamReader (dataPath)
    let csv = new CsvReader(dataFile, new CsvConfiguration(CultureInfo.InvariantCulture))
    let learnDataResult = new Dictionary<string, bool>()
    let testDataResult = new Dictionary<string, bool>()

    let rowsCount = File.ReadLines(dataPath).Count()
    if maxRowsCount > rowsCount then
        maxRowsCount |> ref := rowsCount - 1

    let maxLearnDataCount = ceil(2f / 3f * float32(maxRowsCount))
    let maxTestDataCount = ceil(float32(maxRowsCount) / 3f)

    csv.Read() |> ignore
    csv.ReadHeader() |> ignore
    while csv.Read() do
        let currentDataPiece = (csv.GetField("text"), csv.GetField<bool>("isSpam"))
        if (new Random()).Next(0, 2) = 0 then
            if float32(learnDataResult.Count) < maxLearnDataCount then
                learnDataResult.Add(currentDataPiece) 
        elif float32(testDataResult.Count) < maxTestDataCount then
            testDataResult.Add(currentDataPiece)
        else
            learnDataResult.Add(currentDataPiece) 
        
    [|learnDataResult; testDataResult|]

let getNormalisedWordsProbs (data : Dictionary<string, bool>) = 
    let wordsCountDict = new Dictionary<string, int[]>()
    for item in data do
        let cleanText = item.Key |> String.map(fun c -> 
            if Char.IsLetter(c) || Char.IsWhiteSpace(c) then 
                Char.ToLower(c)
            else 
                ' ')
        for word in cleanText.Split(' ') do
            if (not(String.IsNullOrWhiteSpace(word))) then
                if wordsCountDict.ContainsKey(word) then
                    if item.Value then
                        wordsCountDict.[word].[0] <- wordsCountDict.[word].[0] + 1
                    else 
                        wordsCountDict.[word].[1] <- wordsCountDict.[word].[1] + 1
                elif word.Last() = 's' && wordsCountDict.ContainsKey(word.Substring(0, word.Length - 1)) then
                    if item.Value then
                        wordsCountDict.[word.Substring(0, word.Length - 1)].[0] <- wordsCountDict.[word.Substring(0, word.Length - 1)].[0] + 1
                    else 
                        wordsCountDict.[word.Substring(0, word.Length - 1)].[1] <- wordsCountDict.[word.Substring(0, word.Length - 1)].[1] + 1
                else
                    if item.Value then
                        wordsCountDict.Add(word, [|1; 0|])
                    else 
                        wordsCountDict.Add(word, [|0; 1|])
    
    let normalisedResult = new Dictionary<string, float[]>()
    for kvp in wordsCountDict do
        if (kvp.Value.[0] + kvp.Value.[1]) > 1 then
            let pSpam = (float(kvp.Value.[0]) + 0.5) / float(kvp.Value.[0] + kvp.Value.[1] + 1)
            let pHam = (float(kvp.Value.[1]) + 0.5) / float(kvp.Value.[0] + kvp.Value.[1] + 1)
            let x = [|pSpam; pHam|]
            normalisedResult.Add(kvp.Key, x)
    normalisedResult

[<EntryPoint>]
let main argv =
    let spamProb = 0.5
    let hamProb = 0.5
    let points = new Dictionary<int, float>()
    for i in [|9; 12; 15; 18|] do
        printfn $"Data array count: {i}"
        let learnData = getData(i).[0]
        let testData = getData(i).[1]

        let normalizedProbs = learnData |> getNormalisedWordsProbs
        
        let mutable correctCount = 0
        for test in testData do
            let mutable currentSpamProb = spamProb
            let mutable currentHamProb = hamProb
            let cleanText = test.Key |> String.map(fun c -> 
                if Char.IsLetter(c) || Char.IsWhiteSpace(c) then 
                    Char.ToLower(c)
                else 
                    ' ')
            for word in cleanText.Split(' ') do
                let currentWordProbs = ref [||]
                if normalizedProbs.TryGetValue(word, currentWordProbs) 
                    || normalizedProbs.TryGetValue(word + "s", currentWordProbs)
                    || word.Length > 1 && normalizedProbs.TryGetValue(word.Substring(0, word.Length - 1), currentWordProbs) then
                    currentSpamProb <- currentSpamProb * (!currentWordProbs).[0]
                    currentHamProb <- currentHamProb * (!currentWordProbs).[1]
            printf $"Message: \"{test.Key}\",\n\tpSpam: {currentSpamProb},\n\tpHam: {currentHamProb}.\n\tThe message is considered "
            if (currentSpamProb >= currentHamProb) then
                printf "spam"
                if test.Value then correctCount <- correctCount + 1
            else
                printf "ham"
                if not(test.Value) then correctCount <- correctCount + 1
            printf ". The right answer is "
            if test.Value then printfn "spam" 
            else printfn "ham"
            printf "\n"

        points.Add(i, float(correctCount) / float(testData.Count))

    Chart.Line [for x in points.Keys -> (x, points.[x])] |> Chart.Show
    Console.ReadKey() |> ignore
    0