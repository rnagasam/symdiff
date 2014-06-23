@rem -*- Perl -*-
@setlocal

@if (%PERL_BIN_PATH%) == () ( 
    @perl -x "%~dpnx0" %* 
    @goto :eof
) else (
    @if "%PERL_BIN_PATH%" == "UNDEFINED" (
        @perl -x "%~dpnx0" %* 
        @goto :eof
    ) else (
        @%PERL_BIN_PATH%\perl -x "%~dpnx0" %* 
        @goto :eof
    )
)
@goto :eof
#!perl

use Cwd;

##################################################
# SymDiff regression script
##################################################

# @examples = {(dir, v1, v2), ... }
# $rootdir = <cwd>
# foreach ($dir, $v1, $v2) in @examples
#     chdir $dir
#     run_symdiff_c.cmd $v1 $v2 [options]
#     mv $v1$v2.log $v1$v2.output.log
#     grep Final $v1$v2.output.log
#     Compare (#Procs, #Cex, #noEq, #Crash) with $v1$v2.golden.log
#     chdir $rootdir
#     

####################################################################################################

###################################################
# 
# The following flag controls whether we rerun or just
# compute stats
#
###################################################

   
####################################################################################################

my $pattern = "";


sub MyExec{
    my $cmd = shift;
    print OUTPUT "$cmd \n";
    system("$cmd ");
}

sub GetStats {
  my ($file) = @_;
  open (OUT, "<$file") or die "Can't open $output file\n";
  my $result = "";
  # look for the line below
  # FinalStats: (#Procs, #Cex, #NotEq, #Crash, #Time) = (3,3,3,0,943)
  for my $line (<OUT>) {
#    if ($line =~ /FinalStats:.* = \(([0-9]+),([0-9]+),([0-9]+),([0-9]+),([0-9]+)\)/) {
#       #print "--> $1, $2, $3, $4, $5\n";
#       $result  = "$1, $2, $3, $4";
#     }
    if($line =~ /Verifier\[0\]: Result/) {
      $result = $result . " " . $line;
    }	
 }
  if ($result eq "") { 
    print "Error: Stats not found in $file file\n";
  }	
  close OUT;
  $result
}	

sub CheckRegression {
  my ($golden, $output) = @_;
  my $golden_stats = GetStats($golden);
  my $output_stats = GetStats($output);
  if ($golden_stats eq $output_stats) {
    print "Regression passed\n";
  } else {
    print "Regression failed\n";
  }	
  
}

sub RunSymDiff {
  my ($lang, $ex, $v1, $v2, $o, $regr, $tag) = @_;
  print "[$ex] with $v1 and $v2 with tag $tag\n";
  if ($regr eq -1) { 
    print "Specify either -c or -g...\n";
    return;
  }	
  MyExec("run_symdiff$lang.cmd $v1 $v2 $o > $v1$v2.dmp");
  sleep(2);
  MyExec("mv $v1$v2.log $v1$v2.output$tag.log");
  if ($regr eq 1) { # generate golden files
    print "Generating golden file...\n";
    MyExec("mv $v1$v2.output$tag.log $v1$v2.golden$tag.log");
  } elsif ($regr eq 2) {
    CheckRegression("$v1$v2.golden$tag.log", "$v1$v2.output$tag.log");
  }	
}


sub RunExampleWithOptions {
  my ($lang, $lexamples, $flags, $cwd, $opt_regr, $tag) = @_;
  print "Options = $flags\n";
  for my $ex (@{$lexamples}) {
    my $exdir = $ex->[0];
    next unless ($exdir =~ $pattern);
    my $v1 = $ex->[1];
    my $v2 = $ex->[2];
    my $ex1 = "$cwd\\$exdir";
    chdir ("$ex1") or die "Can't chdir to $ex1\n";
    RunSymDiff($lang, $ex1, $v1, $v2, $flags, $opt_regr,$tag);
    chdir ("$cwd") or die "Can't chdir to back to $cwd\n";
  }
}

my $cwd = getdcwd();
print "***** Current working directory is $cwd ****** \n";


my $opt_regr = -1;
my $error = 0;
sub ProcessOptions {
  if (not (@ARGV)) {
     $error = 1;
  } 
    while($ARGV[0]) {
	my $opt = shift @ARGV;
	if ($opt eq "-g") {
	  $opt_regr = 1;
	} elsif ($opt eq "-c") {
	  $opt_regr = 2;
	} elsif ($opt =~/^-/) {
        }
        else {
          $pattern = $opt;
	  print "Setting pattern as $pattern\n";
	}	
      }	
  }

sub PrintUsage {
  print "Usage: run_regressions.cmd [-c|-g] [pattern]\n";
  print "\t -c: run regressions\n";
  print "\t -g: gen golden files\n";
  print "\t pattern: only run on files with <pattern> as substr of directoryname\n";
}

ProcessOptions();
if (($error eq 1) or ($opt_regr eq -1)) {
  PrintUsage();
  exit(1);
}

##############################################################
## Normal SymDiff example
##############################################################
my @examples = 
  (
    ##### ex3 #########
     ["c_examples\\ex3\\", "v1", "v2"],
    ## ["c_examples\\ex3\\", "v2", "v3"], ## crashes
     ["c_examples\\ex3\\", "v2", "v4"],
    ##### ex4 #########
    ## ["c_examples\\ex4\\", "v1", "v2"], ## nmake crashes
    ##### ex5 #########
     ["c_examples\\ex5\\", "v1", "v2"],
    ##### ex6 #########
    ## ["c_examples\\ex6\\", "v1", "v2"], ## some error
    ##### ex7 #########
     ["c_examples\\ex7\\", "v1", "v2"],
    ##### rtlunicodeStringToIntegerWin8 #########
     ["c_examples\\rtlunicodeStringToIntegerWin8\\", "v1", "v2"],
    ##### print_tokens2 #########
     ["c_examples\\print_tokens2\\", "source", "v3"],
    ##### techfest #########
    ["c_examples\\techfest\\", "v1", "v2"],
    ## ["c_examples\\techfest\\", "v1", "v3"],
    ##### unnamed_structs #########
    ["c_examples\\unnamed_structs\\", "v1", "v2"]
   
  );


# print "--------------------------\n";
# print "Normal SymDiff regressions\n";
# print "--------------------------\n";
# my $flags = "/analyzeChangedCallersOnly /localcheck /cex:1 /nohtml ";
# my $tag = "";
# RunExampleWithOptions("_c", \@examples, $flags, $cwd, $opt_regr, $tag);

# print "---------------------------------------\n";
# print "Normal SymDiff regressions with wrapper\n";
# print "---------------------------------------\n";
# my $flags = "/analyzeChangedCallersOnly /localcheck /cex:1 /nohtml /boogiewrapper ";
# my $tag = "";
# RunExampleWithOptions("_c", \@examples, $flags, $cwd, $opt_regr, $tag);

##############################################################
## Example for RVT
##############################################################
my @rvt_examples = 
  (
    ##### ex_loop #########
     ["c_examples\\ex_loop\\", "v1", "v2"],
    ##### mutual1 #########
    ["c_examples\\mutual1\\", "orig", "v1"],
    ## ["c_examples\\mutual1\\", "orig", "v2"], #raises an exception
    ## ["c_examples\\mutual1\\", "v1", "v2"],  #blows up creating a zillion z3 processes investigate
    ##### print_tokens2 #######
     ["c_examples\\print_tokens2\\", "source", "v3"],
    ##### ex_loop2 #######
     ["c_examples\\ex_loop2\\", "v1", "v2"], #should be verified
    ##### ex_loop2 #######
     ["c_examples\\ex_loop2\\", "v1", "v3"] #should not be verified
  );


# print "--------------------------\n";
# print "RVT SymDiff regressions\n";
# print "--------------------------\n";
# $flags = "/analyzeChangedCallersOnly /rvt /cex:1 /nohtml";
# my $tag = ".rvt";
# RunExampleWithOptions("_c", \@rvt_examples, $flags, $cwd, $opt_regr, $tag);


##############################################################
## Example for BPL
##############################################################
my @bpl_examples = 
  (
    ##### ex1 #######
     ["bpl_examples\\ex1\\", "v1", "v2"],
    ##### swap #######
    ## ["bpl_examples\\swap\\", "v1", "v2"], ## need the right z3 options RELEVANCY
    ##### inline #######
     ["bpl_examples\\inline\\", "v1", "v2"],
    ##### modset #######
     ["bpl_examples\\modset\\", "v1", "v2"],
    ##### datatype #######
     ["bpl_examples\\datatype_danfeng\\", "v1", "v2"],
    ##### datatype #######
     ["bpl_examples\\datatype_danfeng\\", "v3", "v4"]
  );

print "--------------------------\n";
print "BPL SymDiff regressions\n";
print "--------------------------\n";
$flags = "/cex:1 /nohtml";
my $tag = ".bpl";
RunExampleWithOptions("_bpl", \@bpl_examples, $flags, $cwd, $opt_regr, $tag);

##############################################################
## Example for /returnOnly
##############################################################
my @ret_examples = 
  (
    ##### returns_as_outputs #######
     ["bpl_examples\\returns_as_outputs\\", "v1", "v2"]
  );

# print "--------------------------\n";
# print "ReturnOnly SymDiff regressions\n";
# print "--------------------------\n";
# $flags = "/returnOnly /cex:1 /nohtml";
# my $tag = ".retbpl";
# RunExampleWithOptions("_bpl", \@ret_examples, $flags, $cwd, $opt_regr, $tag);

##############################################################
## Example for BPL
##############################################################
my @bpl_examples = 
  (
    ##### ex1 #######
     ["bpl_examples\\nonmodular_asserts\\", "v1", "v2"],
  );

print "--------------------------\n";
print "BPL nonmodular SymDiff regressions\n";
print "--------------------------\n";
$flags = "/cex:1 /nohtml /nonmodular";
my $tag = ".bpl";
RunExampleWithOptions("_bpl", \@bpl_examples, $flags, $cwd, $opt_regr, $tag);
