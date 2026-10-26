namespace DinoReader.Tests;

/// <summary>One free story in the Dino store (Slice 102): its fixed package id, the file stem its translations are stored under, the
/// credit line, the age range and the author the store card and filters use (Slice 112, Claude's drafts for review), the generated cover colour and the characters its speech tags must give a voice.</summary>
internal sealed record StoreStorySpec(string Id, string Title, string Stem, string Text, int ColourIndex, string Credits, int AgeMin, int AgeMax, string Author, IReadOnlyList<string> Characters, int Version, string Added);

/// <summary>
/// The ten free store stories (Slice 102, Decision 106): public-domain tales retold in plain words for ages 3 to 8, with a named
/// speaker on every line so the caster gives each character a voice (the starter rule: <c>said Name</c> or <c>Name said</c>, a bare
/// capitalised name). Pages are separated by a line holding only "---". The retellings are original to this repo (MIT); Peter Rabbit
/// is Beatrix Potter's 1902 text, shortened to fit fifteen pages (a few speech tags added). The ids never change: "Downloaded" and updates match on them.
/// <see cref="StoreBuilder"/> turns them into the files that go up to R2.
/// </summary>
internal static class StoreTexts
{
    public const string LionAndMouseId = "1ca50c6936cc44788b992726784e8132";
    public const string GoldilocksId = "67695905c10a46bd81aa498beab9609e";
    public const string RedRidingHoodId = "1f0f0cb207b244368b7aef26028f4ac5";
    public const string UglyDucklingId = "6f592017f08446eeae7adafa22318660";
    public const string RedHenId = "6eff8d779a9542fcafa76ae58ba1e96b";
    public const string BillyGoatsId = "32d04b935a5649088293314cc648c825";
    public const string BeanstalkId = "e2fb7f5de4ab4c4eb8bd2bb97c91accc";
    public const string ElvesId = "34efea95400343ac998e9ed99158d370";
    public const string TownMouseId = "67dfb7db7c1c418c811cfed88876327f";
    public const string PeterRabbitId = "18cab2f1fec84aad831ba12dccad5336";

    private const string AddedDate = "2026-10-24";
    private const string RetoldAesop = "Retold from Aesop · free to share";
    private const string Traditional = "Traditional";
    private const string RetoldTraditional = "Retold traditional tale · free to share";

    public static IReadOnlyList<StoreStorySpec> All { get; } =
    [
        new(LionAndMouseId, "The Lion and the Mouse", "lion-and-mouse", LionAndMouse, 1, RetoldAesop, 4, 7, "Aesop", ["Lion", "Mouse"], 4, AddedDate),
        new(GoldilocksId, "Goldilocks and the Three Bears", "goldilocks", Goldilocks, 3, RetoldTraditional, 3, 6, Traditional, ["Goldilocks", "Papa Bear", "Mama Bear", "Baby Bear"], 3, AddedDate),
        new(RedRidingHoodId, "Little Red Riding Hood", "red-riding-hood", RedRidingHood, 3, RetoldTraditional, 4, 7, Traditional, ["Red Riding Hood", "Mother", "Wolf", "Grandmother", "Woodcutter"], 3, AddedDate),
        new(UglyDucklingId, "The Ugly Duckling", "ugly-duckling", UglyDuckling, 0, "Retold from Andersen · free to share", 5, 8, "Hans Christian Andersen", ["Mother Duck", "Duckling", "Dilly", "Hen", "Swan"], 4, AddedDate),
        new(RedHenId, "The Little Red Hen", "little-red-hen", RedHen, 3, RetoldTraditional, 3, 6, Traditional, ["Hen", "Dog", "Cat", "Duck"], 3, AddedDate),
        new(BillyGoatsId, "The Three Billy Goats Gruff", "billy-goats-gruff", BillyGoats, 0, RetoldTraditional, 4, 7, Traditional, ["Little Billy", "Middle Billy", "Big Billy", "Troll"], 5, AddedDate),
        new(BeanstalkId, "Jack and the Beanstalk", "jack-and-the-beanstalk", Beanstalk, 2, RetoldTraditional, 5, 8, Traditional, ["Jack", "Mother", "Giant", "Peddler"], 3, AddedDate),
        new(ElvesId, "The Elves and the Shoemaker", "elves-and-shoemaker", Elves, 1, "Retold from the Brothers Grimm · free to share", 5, 8, "Brothers Grimm", ["Hans", "Greta", "Nib", "Nob"], 3, AddedDate),
        new(TownMouseId, "The Town Mouse and the Country Mouse", "town-mouse-country-mouse", TownMouse, 2, RetoldAesop, 4, 7, "Aesop", ["Town Mouse", "Country Mouse"], 3, AddedDate),
        new(PeterRabbitId, "The Tale of Peter Rabbit", "peter-rabbit", PeterRabbit, 3, "Beatrix Potter, 1902 (shortened)", 3, 6, "Beatrix Potter", ["Mrs. Rabbit", "Peter", "Mr. McGregor"], 4, AddedDate),
    ];

    public const string LionAndMouse = """
One hot afternoon a great Lion lay asleep in the shade of a tree. A tiny Mouse was running home through the grass with a berry for her supper.

Pitter, patter, pitter, patter, went her little feet. She did not see the Lion, and she ran right over his nose.
---
The Lion woke up with a roar. He put his big paw down beside the little Mouse.

"Who dares to wake the king of the beasts?" said Lion. "You gave me such a fright!"
---
The Mouse stood up on her back legs and held her paws together.

"Please let me go, great Lion," said Mouse. "I did not mean to wake you. If you are kind to me today, I will be kind to you one day."
---
The Lion laughed so hard that his tummy shook. "You, help me?" said Lion. "You are far too small to help a lion!"
---
But he was a kind king, and he lifted his paw. "Run home, little Mouse," said Lion.

Pitter, patter, pitter, patter, went her little feet.
---
Some days later the Lion went for a walk by the river. A yellow butterfly fluttered past his nose, and he bounced after it.

He was having so much fun that he did not look where he was going.
---
An old fishing net hung between two trees, drying in the sun. Bump! The Lion ran right into it.

The net wrapped all around him, from his nose to the tip of his tail.
---
The Lion wriggled this way and that way. But the more he wriggled, the more tangled he got.

"Grr! This is no fun at all!" said Lion. Then he gave a big, grumpy roar.
---
The Mouse heard the roar from far away. She sat up in her little nest and listened.

"That is the kind Lion," said Mouse. "He sounds stuck! Now it is my turn to be kind to him."
---
She ran through the grass as fast as her legs could go. Pitter, patter, pitter, patter, went her little feet.

"Do not worry, great Lion," said Mouse. "I am coming!"
---
The Mouse stood beside the net and looked up at the Lion.

"You came!" said Lion. "But you are so small. How can you help me?"

"Just wait and see," said Mouse.
---
The Mouse began to nibble the ropes with her sharp little teeth. Nibble, nibble, snip! Nibble, nibble, snip!

She nibbled and nibbled, and one by one the ropes came apart.
---
Soon there was a hole in the net, and the Lion stepped out. He shook his mane, and bits of string flew everywhere.

"Hooray!" said Mouse.
---
The Lion lay down in the grass, nose to nose with the little Mouse.

"Thank you, little friend," said Lion. "I am sorry I laughed at you. Now I know that even a small friend can be a great friend."

"Kind friends help each other," said Mouse with a smile.
---
That evening the Lion lay down under his shady tree, and the Mouse curled up on his big soft paw.

"Goodnight, little friend," said Lion. "Goodnight, great friend," said Mouse.

From that day on, the Lion and the Mouse were the best of friends.
""";

    public const string Goldilocks = """
Once upon a time there was a little girl called Goldilocks. She had long golden hair, and she loved to explore.

One morning she went for a walk in the forest, and she found a little house. The door was open, so she peeked inside.
---
In the house lived three bears. Papa Bear was big, Mama Bear was middle-sized and Baby Bear was very small. They had gone for a walk while their porridge cooled.

On the table stood three bowls of porridge.
---
Goldilocks tasted the porridge in the big bowl. "Oh, this porridge is too hot," said Goldilocks.

She tasted the porridge in the middle bowl. "Oh, this porridge is too cold," said Goldilocks.

Then she tasted the porridge in the little bowl. "Mmm, this porridge is just right," said Goldilocks, and she ate it all up.
---
Goldilocks went into the next room. There were three chairs.

She sat in the big chair. "This chair is too hard," said Goldilocks. She sat in the middle chair. "This chair is too soft," said Goldilocks.

Then she sat in the little chair. "This chair is just right," said Goldilocks. But crack! The little chair broke into pieces.
---
Goldilocks was sleepy, so she went upstairs. There were three beds. She lay on the big bed, but it was too high. She lay on the middle bed, but it was too lumpy. Then she lay on the little bed, and it was just right. Soon she was fast asleep.
---
The three bears came home. Papa Bear looked at the table. "Somebody has been eating my porridge," said Papa Bear.

"Somebody has been eating my porridge," said Mama Bear.

"Somebody has been eating my porridge, and it is all gone!" said Baby Bear.
---
The bears looked at the chairs. "Somebody has been sitting in my chair," said Papa Bear.

"Somebody has been sitting in my chair," said Mama Bear.

"Somebody has been sitting in my chair, and it is broken!" said Baby Bear.

Then the three bears went upstairs.
---
"Somebody has been sleeping in my bed," said Papa Bear.

"Somebody has been sleeping in my bed," said Mama Bear.

"Somebody is sleeping in my bed, and she is still here!" said Baby Bear.
---
Goldilocks woke up and saw the three bears. "Oh dear!" said Goldilocks. She jumped out of the bed, ran down the stairs and out of the door, and she did not stop until she was home.

"I am sorry I ate your porridge," she said to herself. And she never went into the bears' house again.
""";

    public const string RedRidingHood = """
Once upon a time a little girl lived near a big green wood. She always wore a bright red hood that her grandmother had made, so everyone called her Red Riding Hood.

One morning her mother packed a basket of cakes and honey.
---
"Take this basket to your grandmother, who is not feeling well," said Mother. "Go straight along the path, and do not talk to strangers."

"I will be careful," said Red Riding Hood, and off she went.
---
In the wood Red Riding Hood met a big grey Wolf. He had a sly smile and a bushy tail.

"Good morning, little girl," said Wolf. "Where are you going so early?"

"I am taking cakes and honey to my grandmother," said Red Riding Hood. "She lives in the cottage by the old oak tree."
---
"What a lovely idea," said Wolf. "Why not pick some flowers for her too? There are pretty ones in that meadow."

Red Riding Hood stopped to pick flowers. Meanwhile the Wolf ran as fast as he could to the cottage by the old oak tree.
---
Knock, knock! The Wolf knocked on the door. "Who is there?" said Grandmother.

"It is Red Riding Hood," said Wolf in a soft little voice. Grandmother opened the door, and the Wolf jumped inside. Grandmother hid in the big wardrobe, and the Wolf put on her nightcap and climbed into her bed.
---
Soon Red Riding Hood came to the cottage with her flowers. She went in and walked up to the bed.

"Grandmother, what big ears you have!" said Red Riding Hood.

"All the better to hear you with," said Wolf.

"Grandmother, what big eyes you have!" said Red Riding Hood.

"All the better to see you with," said Wolf.
---
"Grandmother, what big teeth you have!" said Red Riding Hood.

"All the better to surprise you with!" said Wolf, and he jumped out of the bed.

"Oh!" cried Red Riding Hood, and she ran out of the door.
---
A Woodcutter was working nearby. He heard the shout and came running.

"Stop right there, Wolf!" said Woodcutter. "That is no way to treat a little girl."

The Wolf took one look at the Woodcutter, and he ran away into the wood as fast as his legs could carry him. He never came back.
---
The Woodcutter opened the wardrobe, and out came Grandmother, quite safe. "Thank you, dear Woodcutter," said Grandmother. "And thank you for coming, my little Red Riding Hood."

"I will never talk to strangers again," said Red Riding Hood.

They all sat down together and ate the cakes and honey.
""";

    public const string UglyDuckling = """
One sunny summer morning, Mother Duck sat on her nest by the pond. Crack, crack, crack! Out popped three fluffy yellow ducklings.

But the biggest egg did not crack at all.
---
At last the big egg went crack! Out came a big grey duckling with a long neck and big flat feet.

"Oh my, what a big baby!" said Mother Duck, and she tucked him under her wing.

"Big or small, I love you all," said Mother Duck.
---
The next morning Mother Duck led her babies down to the pond, one behind the other.

Paddle, paddle, splish, splash! The big grey duckling swam the fastest of all.
---
On the bank the yellow ducklings looked at their big grey brother. Dilly, the smallest, began to giggle.

"Your neck is so long, and your feet are so big!" said Dilly. "You do not look like a duck at all."

The big grey duckling looked down at his feet. He felt a little sad.
---
Mother Duck put her wing around him. "Dilly, that was not kind," said Mother Duck. "Everyone is different, and that is a good thing."

"Big or small, I love you all," said Mother Duck.

Dilly hung her head. "Sorry, big brother," said Dilly. "You are the best swimmer of us all."
---
"But if I do not look like a duck, what kind of bird am I?" said Duckling.

"Go and look around the farm, and you may find out," said Mother Duck. "Be home in time for supper."

So off he waddled across the meadow, waving goodbye.
---
In the farmyard a plump brown Hen was scratching for corn.

"Am I a hen like you?" said Duckling.

"Cluck, cluck! Can you lay an egg?" said Hen. Duckling shook his head.

"Then you are not a hen," said Hen, and she smiled. "But you are a very polite little bird. Now run home for your supper."
---
Soon it was autumn, and the leaves turned red and gold. One day three big white birds with long necks like his flew high over the pond. "Those are swans," said Mother Duck.

"Am I a swan like you?" called Duckling. But the swans were too high to hear him, and they flew far away.
---
Then winter came. Snow fell softly, and the pond turned to ice.

Hen fluffed up her feathers at the barn door. "Come inside, all of you!" said Hen. "It is warm and dry in the barn."

So the ducks waddled through the snow, one behind the other, into the barn.
---
All winter long the ducks and Hen snuggled together in the soft, warm straw. Duckling slept between Mother Duck and Dilly.

"Big or small, I love you all," whispered Mother Duck.
---
At last spring came, and the ice melted away. Duckling had grown big over the winter. He went down to the pond and looked into the water.

There he saw a beautiful white bird with a long, graceful neck. "Is that really me?" said Duckling.
---
Swoosh! Three white swans glided down onto the pond and swam up to him.

"Am I a swan like you?" said Duckling.

"Yes, you are!" said Swan, and he bowed his long neck. "You are a young swan, and a fine one. Welcome, friend!"
---
Mother Duck and the ducklings came paddling out to see. "Will you swim with my family too?" said Duckling.

"Of course," said Swan. "The pond is big enough for everyone."

Paddle, paddle, splish, splash! Ducks and swans swam round the pond together.
---
That evening the sun went down, pink and gold. Dilly and the other ducklings rode home on their big brother's back.

"You are a swan, and you are still our brother," said Dilly.

"Big or small, I love you all," said Mother Duck. And Duckling was happier than he had ever been.
""";

    public const string RedHen = """
Once upon a time a little red Hen lived on a farm. She shared her house with a lazy Dog, a sleepy Cat and a noisy Duck. The Hen worked hard every day, but the others did nothing at all.
---
One morning Hen found some grains of wheat. "Who will help me plant this wheat?" said Hen.

"Not I," said Dog.

"Not I," said Cat.

"Not I," said Duck.

"Then I will do it myself," said Hen. And she did.
---
The wheat grew tall and golden. "Who will help me cut the wheat?" said Hen.

"Not I," said Dog.

"Not I," said Cat.

"Not I," said Duck.

"Then I will do it myself," said Hen. And she did.
---
Next it was time to take the wheat to the mill. "Who will help me grind the wheat into flour?" said Hen.

"Not I," said Dog.

"Not I," said Cat.

"Not I," said Duck.

"Then I will do it myself," said Hen. And she did.
---
Hen took the flour home. "Who will help me bake the bread?" said Hen.

"Not I," said Dog.

"Not I," said Cat.

"Not I," said Duck.

"Then I will do it myself," said Hen. And she did.
---
Soon the little house smelled of warm, fresh bread. The Dog, the Cat and the Duck came running into the kitchen.
---
"Who will help me eat the bread?" said Hen.

"I will!" said Dog.

"I will!" said Cat.

"I will!" said Duck.
---
"No, you will not," said Hen. "I planted the wheat, I cut it, I ground it and I baked the bread. Now I will eat it myself, with my little chicks."

Dog, Cat and Duck hung their heads. They had not helped at all.
---
The next day Hen found more wheat. "Who will help me plant this wheat?" said Hen.

"I will!" said Dog.

"I will!" said Cat.

"I will!" said Duck.

And this time they did, and they shared the bread together.
""";

    public const string BillyGoats = """
Once upon a time three Billy Goats lived on a hill. Their name was Gruff. Little Billy was the smallest, Middle Billy was a bit bigger, and Big Billy was the biggest of all.

The grass on their hill was getting short. Across the river there was a meadow full of sweet green grass.
---
To get to the meadow, the goats had to cross a wooden bridge. And under the bridge lived a grumpy Troll, with eyes like saucers and a nose as long as a carrot.

He sat there all alone, day after day.
---
Little Billy went first. Trip, trap, trip, trap, went his little hooves on the bridge.

"Who is that trip-trapping over my bridge?" said Troll.

"It is only me, Little Billy Goat Gruff," said Little Billy in a tiny voice. "I am going to the meadow to eat grass."
---
"Nobody crosses my bridge without a stamping match!" said Troll.

"Oh, please let me cross," said Little Billy. "I am too small to stamp. Wait for my brother, who is much bigger than me."

"Well, be off with you," said Troll. And Little Billy skipped across the bridge to the meadow.
---
Then Middle Billy came. Trip, trap, trip, trap, went his hooves on the bridge.

"Who is that trip-trapping over my bridge?" said Troll.

"It is only me, Middle Billy Goat Gruff," said Middle Billy. "I am going to the meadow to eat grass."
---
"Nobody crosses my bridge without a stamping match!" said Troll.

"Oh, please let me cross," said Middle Billy. "I am too small to stamp. Wait for my brother, who is much bigger than me."

"Very well, be off with you," said Troll. And Middle Billy trotted across the bridge to the meadow.
---
At last Big Billy came. TRIP, TRAP, TRIP, TRAP, went his big hooves on the bridge. The whole bridge shook.

"Who is that trip-trapping over my bridge?" said Troll.

"It is me, Big Billy Goat Gruff," said Big Billy in a deep, loud voice.
---
"Nobody crosses my bridge without a stamping match!" said Troll, and he stamped onto the bridge.

Stamp, stamp, stamp, went his big feet. Big Billy stood still and waited for his turn.
---
But the old boards were wet and slippery. The Troll wobbled this way and that way.

Splash! He slid off the low bridge and sat down in the shallow river.
---
"Are you all right?" said Big Billy.

The water was shallow and cool, and it only came up to the Troll's tummy. A little fish tickled his toes, and he began to laugh.

"Ho, ho, ho! That tickles!" said Troll. And the three Billy Goats laughed too.
---
The soggy Troll climbed out onto the bank below the hill and sat down. Big Billy came down from the bridge and sat beside him.

"Why are you so grumpy?" said Big Billy. "I think you are just lonely."

"Nobody ever asks me to come along," said Troll.
---
"Come across with us and taste the sweet green grass," said Big Billy.

Big Billy and the Troll crossed the bridge to join the other goats in the meadow.
---
The Troll nibbled a little grass, and it was so good that he gave a big, happy smile.

"Thank you, Billy Goats," said Troll. "This is the best day I have ever had."
---
The three Billy Goats Gruff and their new friend ate sweet green grass all summer long.

Every evening the goats went trip, trap, home over the bridge, and the Troll waved goodnight.

"Come back tomorrow!" said Troll.

Snip, snap, snout, this tale's told out.
""";

    public const string Beanstalk = """
Once upon a time a boy called Jack lived with his mother in a little cottage. They were very poor. All they had left was a cow named Milky-White, and the milk had run dry.

"We must sell the cow, Jack," said Mother. "Take her to market and bring home a good price."
---
On the way Jack met a funny little Peddler. "That is a fine cow," said Peddler. "I will give you five magic beans for her."

"Magic beans?" said Jack. He thought it was a wonderful bargain, so he handed over Milky-White and ran home with the beans.
---
"Five beans?" said Mother. "Oh, Jack, how silly you are!" She threw the beans out of the window and sent Jack to bed.

In the morning Jack looked out of the window. A giant beanstalk had grown up in the night, so tall that it went right up into the clouds.
---
Jack began to climb. Up and up he went, past the birds and into the clouds. At the top he found a long road and a huge castle.

A giant woman opened the door. "Oh dear, my husband does not like visitors," said Giant's Wife. "But you look so hungry that I will give you some bread first."
---
Boom, boom, boom! The whole castle shook. "Fee, fi, fo, fum!" said Giant. "I smell a boy!"

"Nonsense, dear," said Giant's Wife. "It is only the sheep." She hid Jack in the cupboard, and the Giant sat down to eat.
---
After supper the Giant took out a bag of gold coins and counted them. Soon he began to nod, and then he was fast asleep and snoring. Jack crept out of the cupboard, grabbed the bag of gold and ran down the beanstalk as fast as he could.

"Look, Mother!" said Jack. "We are rich!"
---
When the gold was spent, Jack climbed the beanstalk again. This time the Giant had a little hen that laid golden eggs. "Lay!" said Giant, and the hen laid a golden egg.

When the Giant fell asleep, Jack tucked the hen under his arm and tiptoed away.
---
Jack went up a third time. The Giant had a golden harp that played by itself. Jack took it, but the harp cried out. "Master, master!" said Harp.

The Giant woke up and ran after him. "Fee, fi, fo, fum!" said Giant.
---
Jack slid down the beanstalk as fast as he could. The Giant tried to follow, but the beanstalk wobbled and swayed. "Oh dear, I am too scared to climb down," said Giant. So he went back to his castle in the clouds and never came down again.

Jack and his mother lived happily ever after, with the little hen and the golden harp.
""";

    public const string Elves = """
Once upon a time there was a kind Shoemaker named Hans. He made good shoes, but he was very poor. One evening he had leather for only one more pair of shoes.

"I will cut the leather tonight and sew it in the morning," said Hans, and he went to bed.
---
In the morning Hans came into his workshop. On the table stood a pair of shoes, neatly stitched. "Look, Greta," said Hans. "Who could have made them?"

"They are the finest shoes I have ever seen," said Greta.
---
That very day a rich man came into the shop. He liked the shoes so much that he paid more than the usual price. Now Hans had enough money to buy leather for two more pairs of shoes.

He cut out the leather and left it on the table.
---
Again the next morning the shoes were ready, sewn with tiny, perfect stitches. Again they sold at once, and Hans bought leather for four pairs. Night after night the shoes appeared, and soon the shop was full of happy customers.
---
Just before Christmas Hans spoke to his wife. "Greta, let us stay up tonight and see who is helping us," said Hans.

"What a good idea," said Greta. They hid behind a curtain and waited.
---
At midnight two tiny elves came skipping in. They had no clothes, only ragged scraps. They sat at the table and began to stitch and hammer, quick as lightning.

"Tap, tap, tap!" said Nib.

"Stitch, stitch, stitch!" said Nob.
---
When all the shoes were finished, the elves ran away. "Those elves have made us rich," said Greta. "Let us make them something to say thank you. I will sew them little shirts and trousers."

"And I will make them each a pair of tiny shoes," said Hans.
---
On Christmas Eve they laid the gifts on the table. At midnight the elves came skipping in, and they found the clothes. "Oh, how fine we look!" said Nib.

"Now we need not work any more," said Nob. They danced out of the door and never came back, but Hans and Greta were happy for the rest of their lives.
""";

    public const string TownMouse = """
Once upon a time two mice were cousins. Town Mouse lived in a big house in the city, and Country Mouse lived in a cosy hole in a field.

One day Town Mouse came to visit. Country Mouse was so pleased that he gave her the best food he had.
---
Country Mouse brought out barley, a few nuts and a little bit of cheese. "Please eat, dear cousin," said Country Mouse.

Town Mouse nibbled a nut and wrinkled her nose. "Is this all you have to eat?" said Town Mouse. "You must come and see how I live."
---
So the two mice set off to the city. When they reached the big house, Town Mouse led Country Mouse into the dining room. There was a long table with cakes, jelly, fruit and cheese.

"Help yourself," said Town Mouse.
---
Country Mouse had never seen so much food. "What a feast!" said Country Mouse. "You are so lucky to live here."

He was just about to take a bite of cake when the door burst open. In came a big, curious Cat.
---
"Run!" said Town Mouse. The two mice dashed under the table and hid behind a cupboard. They held very still, and their little noses twitched.

The cat padded away at last.
---
"Is it always like this?" said Country Mouse.

"Oh, yes," said Town Mouse. "There is always a cat or a dog or a broom. But the food is so good that it is worth it."

They crept out, but the door opened again, and in came a big, barking Dog.
---
Country Mouse had seen enough. "Thank you for the feast, cousin," said Country Mouse. "But I would rather have a nut in peace than a cake in a fuss."

"Then go home, dear cousin," said Town Mouse. "I will stay here."
---
Country Mouse hurried back to his quiet field, and he was very glad to be home. He ate his barley and nuts under the warm sun, and he had never tasted anything so good.

Simple food in peace is better than a feast in a fuss.
""";

    public const string PeterRabbit = """
Once upon a time there were four little Rabbits, and their names were Flopsy, Mopsy, Cotton-tail and Peter. They lived with their Mother in a sand-bank, underneath the root of a very big fir-tree.
---
"Now, my dears," said Mrs. Rabbit one morning, "you may go into the fields or down the lane, but don't go into Mr. McGregor's garden. Mr. McGregor is a grumpy gardener, and he does not like rabbits in his garden."

"Now run along, and don't get into mischief. I am going out," said Mrs. Rabbit.
---
Then old Mrs. Rabbit took a basket and her umbrella, and went through the wood to the baker's. Flopsy, Mopsy and Cotton-tail, who were good little bunnies, went down the lane to gather blackberries.

But Peter, who was very naughty, ran straight away to Mr. McGregor's garden, and squeezed under the gate!
---
First he ate some lettuces and some French beans, and then he ate some radishes. And then, feeling rather full, he went to look for some parsley.

But round the end of a cucumber frame, whom should he meet but Mr. McGregor!
---
Mr. McGregor was on his hands and knees planting out young cabbages, but he jumped up and ran after Peter, waving his arms. "Stop thief!" said Mr. McGregor.

Peter was in a great hurry. He scampered all over the garden, for he had forgotten the way back to the gate. He lost one of his shoes among the cabbages, and the other shoe amongst the potatoes.
---
Peter felt lost. But some friendly sparrows saw him, and flew to him in great excitement, and cheered him on. "Come along, Peter! You can do it!" they chirped.

Mr. McGregor came hurrying up with a rake. But Peter wriggled out of his little jacket just in time, leaving it behind him.
---
And he rushed into the tool-shed, and jumped into a can. It would have been a beautiful thing to hide in, if it had not had so much water in it.

Presently Peter sneezed. "Kertyschoo!" said Peter. Mr. McGregor was after him in no time, but Peter jumped out of a window, upsetting three plants. The window was too small for Mr. McGregor, and he was tired of running after Peter. He went back to his work.
---
Peter sat down to rest. He was out of breath, and he had not the least idea which way to go. Also he was very damp with sitting in that can.

After a time he began to wander about, going lippity, lippity, not very fast, and looking all around.
---
He went back towards the tool-shed, but suddenly, quite close to him, he heard the noise of a hoe, scr-r-ritch, scratch, scratch, scritch. Peter scuttered underneath the bushes. But presently, as nothing happened, he came out, and climbed upon a wheelbarrow and peeped over.

The first thing he saw was Mr. McGregor hoeing onions. His back was turned towards Peter, and beyond him was the gate!
---
Peter got down very quietly off the wheelbarrow, and started running as fast as he could go, along a straight walk behind some black-currant bushes.

Mr. McGregor caught sight of him at the corner, but Peter did not care. He slipped underneath the gate, and was safe at last in the wood outside the garden.
---
Mr. McGregor hung up the little jacket and the shoes for a scare-crow.

Peter never stopped running or looked behind him till he got home to the big fir-tree. He was so tired that he flopped down upon the nice soft sand on the floor of the rabbit-hole, and shut his eyes.
---
His mother was busy cooking. She wondered what he had done with his clothes. It was the second little jacket and pair of shoes that Peter had lost in a fortnight!

I am sorry to say that Peter was not very well during the evening. His mother put him to bed, and made some camomile tea. "One table-spoonful to be taken at bedtime," said Mrs. Rabbit.
---
But Flopsy, Mopsy, and Cotton-tail had bread and milk and blackberries for supper.
""";
}
