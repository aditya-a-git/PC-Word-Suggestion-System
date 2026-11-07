# Third-party notices

## English Speller Database (ESDB)

The bundled `esdb-en-US.tsv` vocabulary is derived from the official US English Speller Database word list. The source and transformation are recorded in repository history; the source file was pinned to `en-wl/wordlist-diff` release `rel-2026.02.25`, commit `71d7dd07676edb60ade43552e10b41314b7e9287`. The generated output contains 79,343 entries. It is not a frequency corpus; each entry uses a neutral weight.

Copyright 2000-2026 by Kevin Atkinson

Permission to use, copy, modify, distribute, and sell any part of the English Speller Database (ESDB, previously known as SCOWLv2), or word lists created from it, is hereby granted without fee, provided that the above copyright notice appears in all copies and that both the above copyright notice and this notice appear in supporting documentation. Kevin Atkinson makes no representations about the suitability of this database for any purpose. It is provided "as is" without express or implied warranty.

ESDB is derived from many sources, most of which are in the Public Domain. Data from the Corpus of Contemporary American English (COCA) was also used. All data from COCA comes from 3-gram data that is not freely available; however, the usage is within the rights given by the NDA that was signed when purchasing the data. More information on COCA is available at https://www.english-corpora.org/coca/.

The primary source of words for ESDB comes from 12dicts and ENABLE2K. Both are in the Public Domain, but Alan Beale <biljir@pobox.com> deserves special credit as he is the author of 12dicts and a major contributor to ENABLE2K. In addition, he gave Kevin Atkinson feedback and created special lists that helped improve ESDB.

Upstream notice: [en-wl/wordlist Copyright](https://github.com/en-wl/wordlist/blob/v2/Copyright). The selected artifact is the official US speller list; the upstream notice states that this notice is sufficient for a non-Australian official speller dictionary.

## WikiText-2

The WikiText-2 dataset is not bundled. The dataset card reports conflicting license metadata (CC BY-SA 3.0 and GFDL; the card also refers to CC BY-SA 4.0). Do not redistribute the dataset or corpus-trained model until the applicable terms are resolved. Source: [Salesforce WikiText dataset card](https://huggingface.co/datasets/Salesforce/wikitext).