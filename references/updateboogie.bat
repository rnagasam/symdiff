echo Usage: %~n0 [Boogie Binary Dir]
if "%1"=="" (
  echo No directory given.
  exit /b
) else (
  echo Copying Boogie binaries from %1:
)
copy %1\AbsInt.dll .
copy %1\AbsInt.pdb .
copy %1\AIFramework.dll .
copy %1\AIFramework.pdb .
copy %1\Basetypes.dll .
copy %1\Basetypes.pdb .
copy %1\CodeContractsExtender.dll .
copy %1\CodeContractsExtender.pdb .
copy %1\Concurrency.dll .
copy %1\Concurrency.pdb .
copy %1\Core.dll .
copy %1\Core.pdb .
copy %1\ExecutionEngine.dll .
copy %1\ExecutionEngine.pdb .
copy %1\Graph.dll .
copy %1\Graph.pdb .
copy %1\Houdini.dll .
copy %1\Houdini.pdb .
copy %1\Model.dll .
copy %1\Model.pdb .
copy %1\ParserHelper.dll .
copy %1\ParserHelper.pdb .
copy %1\Provers.SMTLib.dll .
copy %1\Provers.SMTLib.pdb .
copy %1\VCExpr.dll .
copy %1\VCExpr.pdb .
copy %1\VCGeneration.dll .
copy %1\VCGeneration.pdb .
REM ------ other boogie files
copy %1\boogie.exe .
copy %1\boogie.pdb .
copy %1\BVD.exe .
copy %1\BVD.pdb .
copy %1\Doomed.dll .
copy %1\Doomed.pdb .
copy %1\Predication.dll .
copy %1\Predication.pdb .
