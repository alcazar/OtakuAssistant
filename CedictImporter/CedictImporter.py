import re
import os

cedict_syntax = re.compile(
    r"\A(?P<traditional>\S+)\s+(?P<simplified>\S+)\s*"
    r"\[(?P<pinyins>.+?)\]\s*/(?P<translations>.+)/?\Z"
)

accents = list(map(lambda s : s[:-1], ["̄ ", "́ ", "̌ ", "̀ "]))

def accentuate(pinyin):
    parts = pinyin.split(" ")
    last_accent = ""
    for i in range(len(parts)):
        part = parts[i]
        if part == "":
            pass
        elif part == "r5":
            if i > 0:
                parts[i-1] += "r"
                part = ""
            else:
                part = "r"
        elif part == "xx5":
            part = "xx"
        else:
            accent = ""
            try:
                # check if the part ends with a number
                accent_index = int(part[-1])
                if accent_index < 5:
                    accent = accents[accent_index - 1]

                part = part[:-1]
            except ValueError:
                pass

            if part[-1] == ":":
                accent = "̈ "[0] + accent
                part = part[:-1]
            else:
                # we have stuff like lu:e4 with an accent on the u and on the e
                # this only happens with u: so quick fix
                part = part.replace("u:", "ü")

            last_accent = accent
            if accent != "":

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

        parts[i] = part

    return " ".join(filter(lambda s : len(s) > 0, parts))

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
                xml_out.write("        <Hanzi>{0}</Hanzi>\n".format(simplified.strip()))
        
                if traditional != simplified: 
                    xml_out.write("        <Traditional>{0}</Traditional>\n".format(traditional.strip()))
                
                meanings = list(meanings.items())
                meanings.sort(key = lambda meaning : len(meaning[1]), reverse = True)
                for pinyins, translations in meanings:
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
