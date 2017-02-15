import re
import os

cedict_syntax = re.compile(
    r"\A(?P<traditional>\S+)\s+(?P<simplified>\S+)\s*"
    r"\[(?P<pinyins>.+?)\]\s*/(?P<translations>.+)/?\Z"
)

accents = list(map(lambda s : s[:-1], ["̄ ", "́ ", "̌ ", "̄ ", "̈̌ "]))

def accentuate(pinyin):
    parts = pinyin.split(" ")
    for i in range(len(parts)):
        part = parts[i]
        if part == "":
            pass
        elif part == "r5":
            part = "r"
        elif part == "xx5":
            part = "xx"
        else:
            try:
                # check if the part ends with a number
                accent = accents[int(part[-1]) - 1]

                lpart = part.lower()
                apos = lpart.find("a") + 1
                epos = lpart.find("e") + 1
                opos = lpart.find("o") + 1
                upos = lpart.find("u") + 1
                ipos = lpart.find("i") + 1
                mpos = lpart.find("m") + 1

                if apos > 0:
                    # if a is present it takes the accent
                    part = part[:apos] + accent + part[apos:]
                elif epos > 0:
                    # if e is present it takes the accent
                    # a and e never appears together
                    part = part[:epos] + accent + part[epos:]
                elif opos > 0:
                    # o takes the accent unless it is with a or o
                    part = part[:opos] + accent + part[opos:]
                elif upos > 0 and ipos > 0:
                    # if u and i appears together, the second one takes the accent
                    pos = max(upos, ipos)
                    part = part[:pos] + accent + part[pos:]
                elif upos > 0:
                    # single u takes the accent
                    part = part[:upos] + accent + part[upos:]
                elif ipos > 0:
                    # single i takes the accent
                    part = part[:ipos] + accent + part[ipos:]
                elif mpos > 0:
                    # m alone can takes the accent
                    part = part[:mpos] + accent + part[mpos:]
                else:
                    raise Exception("No vowel to accentuate in {0} '{1}'".format(part, pinyin))
                
                # remove extra left over space
                part = part[:-1]

            except ValueError:
                # no accent for this one
                pass
        parts[i] = part

    return " ".join(parts)

inline_pinyin = re.compile(r"\[([a-zA-Z0-9 ]+)\]")
def accentuate_inline_pinyin(str):
    return inline_pinyin.sub(lambda m : "[{0}]".format(accentuate(m.group(1))), translation)

with open("cedict_ts.u8", "r", encoding="utf-8") as cedict_in:
    
    words = {}

    print("Parsing cedict input")

    i = 0
    for line in cedict_in:
        match = cedict_syntax.match(line.strip())
        
        if match != None:
            traditional     = match.group("traditional")
            simplified      = match.group("simplified")
            pinyins         = match.group("pinyins").lower()
            translations    = list(filter(lambda s : s != "", map(lambda s : s.strip(), match.group("translations").split("/"))))

            word_key = (traditional, simplified)
            try:
                meanings = words[word_key]
            except KeyError:
                meanings = words[word_key] = {}

            try:
                meanings[pinyins] += translations
            except KeyError:
                meanings[pinyins] = translations

            i += 1
            if i % 1000 == 0:
                print(i)
    print(i)

    cedict_in.close()

    print("Parsing cedict input finished")

    print("{0} words!".format(len(words)))

    word_items = list(words.items())
    word_items.sort()
    part_size = 30000
    i = 0
    print("Writing xml output")
    os.makedirs("../OtakuAssistant/Dictionaries/Cedict_CN_ENG", exist_ok=True)
    for part in range(0, len(word_items)//part_size + 1):
        with open("../OtakuAssistant/Dictionaries/Cedict_CN_ENG/{0:02d}.xml".format(part), "w", encoding="utf-8") as xml_out:
            print("Writing part {0}".format(part))

            xml_out.write("""<?xml version="1.0" encoding="utf-8"?>\n""")
            xml_out.write("""<WordList xmlns="../Dictionary.xsd">\n""")

            for (traditional, simplified), meanings in word_items[part*part_size:(part+1)*part_size]:
                xml_out.write("    <Word>\n")
                xml_out.write("        <Name>{0}</Name>\n".format(simplified.strip()))
        
                if traditional != simplified: 
                    xml_out.write("        <Traditional>{0}</Traditional>\n".format(traditional.strip()))

                for pinyins, translations in meanings.items():
                    xml_out.write("        <Meaning>\n")
                    for pinyin in pinyins.split(","):
                        xml_out.write("            <Pinyin>{0}</Pinyin>\n".format(accentuate(pinyin.strip())))
                    for translation in translations:
                        xml_out.write("            <Translation>{0}</Translation>\n".format(accentuate_inline_pinyin(translation).replace("<", "&lt;").replace(">", "&gt;").replace("&", "&amp;")))
                    xml_out.write("        </Meaning>\n")

                xml_out.write("    </Word>\n")

                i += 1
                if i % 1000 == 0:
                    print(i)
            xml_out.write("""</WordList>""")

    print(i)
    print("Writing XML output finished")
