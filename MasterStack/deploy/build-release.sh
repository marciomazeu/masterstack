#!/bin/bash
echo "Removendo publicacoes antigas..."
rm -rf ../publish

echo "Compilando o MasterStack em modo Release..."
dotnet publish ../MasterStack.csproj -c Release -o ../publish

echo "Pronto! Os arquivos de producao estao na pasta /publish na raiz do projeto."